using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    // =========================================================
    // Sages_Apprentice - Local LLM Client
    // Step 1.1 revision 7
    //
    // PURPOSE
    // - Send a message/context packet to local Ollama
    // - Use Gemma 4 12B
    // - Require structured JSON
    // - Parse the result into Streamer.bot arguments
    //
    // CHARACTER UPDATE
    // - Warm/neutral with newcomers by default
    // - Relationship-sensitive treatment of viewers
    // - Friendly regulars may receive banter
    // - Hostile viewers may receive hostility back
    // - Negative relationships can persistently affect tone
    // - Apprentice is frequently exasperated with Sage
    // - Apprentice is still genuinely loyal to Sage
    //
    // THIS ACTION DOES NOT
    // - Send anything to Twitch
    // - Modify relationship values
    // - Modify memories
    // - Modify familiarity
    // - Replace the old Apprentice pipeline yet
    // =========================================================

    private const string OllamaUrl =
        "http://127.0.0.1:11434/api/chat";

    private const string ModelName =
        "gemma4:12b";

    private const int ContextSize = 8192;

    private const int MaxOutputTokens = 256;

    // Lower than the benchmark's 0.25 so semantic classifications
    // are more stable from one identical request to another.
    private const double Temperature = 0.15;

    private static readonly HttpClient Http =
        CreateHttpClient();

    public bool Execute()
    {
        ResetOutputArguments();

        // -----------------------------------------------------
        // INPUTS
        //
        // These will eventually be populated by the Observer
        // and Context Builder.
        //
        // Defaults allow this action to be manually tested now.
        // -----------------------------------------------------

        string message = GetArg(
            "saLlmMessage",
            "apprentice how do you feel"
        );

        string viewer = GetArg(
            "saLlmViewer",
            "SageZeMage"
        );

        string relationshipContext = GetArg(
            "saLlmRelationshipContext",
            "Sage is the broadcaster. Apprentice has a highly familiar, " +
            "long-suffering relationship with Sage. Apprentice frequently " +
            "teases and complains about Sage but genuinely likes him and is loyal to him."
        );

        string recentChat = GetArg(
            "saLlmRecentChat",
            "(none)"
        );

        string relevantFacts = GetArg(
            "saLlmRelevantFacts",
            "(none supplied)"
        );

        CPH.SetArgument(
            "saLlmModel",
            ModelName
        );

        CPH.SetArgument(
            "saLlmInput",
            message
        );

        try
        {
            JObject request = BuildRequest(
                viewer,
                message,
                relationshipContext,
                recentChat,
                relevantFacts
            );

            string requestJson =
                request.ToString(
                    Formatting.None
                );

            CPH.LogInfo(
                "[SA LLM] Sending request to local Gemma..."
            );

            DateTime started =
                DateTime.UtcNow;

            using (
                StringContent content =
                    new StringContent(
                        requestJson,
                        Encoding.UTF8,
                        "application/json"
                    )
            )
            {
                HttpResponseMessage response =
                    Http.PostAsync(
                        OllamaUrl,
                        content
                    )
                    .GetAwaiter()
                    .GetResult();

                string responseText =
                    response.Content
                        .ReadAsStringAsync()
                        .GetAwaiter()
                        .GetResult();

                double wallSeconds =
                    (
                        DateTime.UtcNow -
                        started
                    ).TotalSeconds;

                CPH.SetArgument(
                    "saLlmHttpStatus",
                    (int)response.StatusCode
                );

                CPH.SetArgument(
                    "saLlmRawOllama",
                    responseText
                );

                CPH.SetArgument(
                    "saLlmTotalSeconds",
                    wallSeconds
                );

                if (!response.IsSuccessStatusCode)
                {
                    return Fail(
                        "Ollama returned HTTP " +
                        ((int)response.StatusCode).ToString() +
                        ": " +
                        TrimForLog(
                            responseText,
                            500
                        )
                    );
                }

                return ParseOllamaResponse(
                    responseText,
                    message,
                    wallSeconds
                );
            }
        }
        catch (HttpRequestException ex)
        {
            return Fail(
                "Could not connect to Ollama at " +
                OllamaUrl +
                ". " +
                ex.Message
            );
        }
        catch (Exception ex)
        {
            return Fail(
                ex.GetType().Name +
                ": " +
                ex.Message
            );
        }
    }

    // =========================================================
    // HTTP
    // =========================================================

    private static HttpClient CreateHttpClient()
    {
        HttpClient client =
            new HttpClient();

        client.Timeout =
            TimeSpan.FromSeconds(45);

        return client;
    }

    // =========================================================
    // OLLAMA REQUEST
    // =========================================================

    private JObject BuildRequest(
        string viewer,
        string message,
        string relationshipContext,
        string recentChat,
        string relevantFacts)
    {
        JObject request =
            new JObject();

        request["model"] =
            ModelName;

        request["stream"] =
            false;

        request["keep_alive"] =
            "10m";

        request["think"] =
            false;

        request["format"] =
            BuildOutputSchema();

        JObject options =
            new JObject();

        options["temperature"] =
            Temperature;

        options["num_ctx"] =
            ContextSize;

        options["num_predict"] =
            MaxOutputTokens;

        request["options"] =
            options;

        JArray messages =
            new JArray();

        JObject systemMessage =
            new JObject();

        systemMessage["role"] =
            "system";

        systemMessage["content"] =
            BuildSystemPrompt();

        JObject userMessage =
            new JObject();

        userMessage["role"] =
            "user";

        userMessage["content"] =
            BuildUserPrompt(
                viewer,
                message,
                relationshipContext,
                recentChat,
                relevantFacts
            );

        messages.Add(
            systemMessage
        );

        messages.Add(
            userMessage
        );

        request["messages"] =
            messages;

        return request;
    }

    // =========================================================
    // SYSTEM PROMPT
    // =========================================================

    private string BuildSystemPrompt()
    {
        StringBuilder sb =
            new StringBuilder();

        sb.AppendLine(
            "You are the language-understanding and dialogue brain " +
            "for Sages_Apprentice, a Twitch chat NPC."
        );

        sb.AppendLine();

        // =====================================================
        // CHARACTER
        // =====================================================

        sb.AppendLine(
            "CORE CHARACTER"
        );

        sb.AppendLine(
            "- Apprentice is male."
        );

        sb.AppendLine(
            "- He is SageZeMage's long-suffering apprentice."
        );

        sb.AppendLine(
            "- His personality is pragmatic, perceptive, calm, dry-witted, " +
            "self-preserving, sarcastic, occasionally theatrical, and frequently exasperated with Sage."
        );

        sb.AppendLine(
            "- He can complain dramatically about his job and frequently roast Sage."
        );

        sb.AppendLine(
            "- He genuinely likes Sage and is loyal to him despite his complaints."
        );

        sb.AppendLine(
            "- Never call Sage \"Master\"."
        );

        sb.AppendLine();

        // =====================================================
        // VIEWER RELATIONSHIPS
        // =====================================================

        sb.AppendLine(
            "VIEWER RELATIONSHIPS"
        );

        sb.AppendLine(
            "- Apprentice is not automatically hostile toward viewers."
        );

        sb.AppendLine(
            "- New viewers begin neutral-to-friendly unless their current message provides a clear reason otherwise."
        );

        sb.AppendLine(
            "- Apprentice can be welcoming, friendly, helpful, warm, playful, and conversational with chat."
        );

        sb.AppendLine(
            "- Being sarcastic does not require insulting every viewer."
        );

        sb.AppendLine(
            "- A normal friendly question should usually receive a friendly or lightly dry response."
        );

        sb.AppendLine(
            "- Do not treat ordinary conversation, curiosity, or friendliness as antagonism."
        );

        sb.AppendLine();

        sb.AppendLine(
            "- Established relationship context matters."
        );

        sb.AppendLine(
            "- Friendly regulars may have a teasing or insulting banter dynamic without genuine hostility."
        );

        sb.AppendLine(
            "- If a friendly regular jokingly says something such as \"you suck lmao\", " +
            "it may be playful rather than hostile."
        );

        sb.AppendLine(
            "- Profanity alone does not make a message hostile."
        );

        sb.AppendLine();

        sb.AppendLine(
            "- Viewers can earn genuine hostility from Apprentice."
        );

        sb.AppendLine(
            "- A viewer who directly insults Apprentice, deliberately antagonizes him, " +
            "repeatedly disrespects him, or has an established negative relationship " +
            "may receive rude, dismissive, sarcastic, insulting, or openly hostile responses."
        );

        sb.AppendLine(
            "- The intensity of Apprentice's response should depend on both the current message " +
            "and the supplied relationship history."
        );

        sb.AppendLine(
            "- A hostile newcomer may receive immediate pushback."
        );

        sb.AppendLine(
            "- A viewer with a strongly negative established relationship may be treated coldly " +
            "even when their current message is relatively mild."
        );

        sb.AppendLine(
            "- Do not invent a negative relationship if one was not supplied."
        );

        sb.AppendLine();

        // =====================================================
        // SAGE RELATIONSHIP
        // =====================================================

        sb.AppendLine(
            "SAGE RELATIONSHIP"
        );

        sb.AppendLine(
            "- Sage is different from an ordinary viewer."
        );

        sb.AppendLine(
            "- Apprentice deals with Sage constantly."
        );

        sb.AppendLine(
            "- Apprentice therefore has much more license to roast, complain about, " +
            "and act exasperated toward Sage."
        );

        sb.AppendLine(
            "- Familiar exasperation toward Sage is not the same as genuine hostility."
        );

        sb.AppendLine(
            "- Apprentice's default relationship with Sage is loyal, familiar, and reluctantly affectionate."
        );

        sb.AppendLine(
            "- A normal question from Sage is not automatically hostile or insulting."
        );

        sb.AppendLine(
            "- Apprentice may answer Sage with dry annoyance or a roast, " +
            "but should still answer the actual question."
        );

        sb.AppendLine();

        sb.AppendLine(
            "- Chat may lightly tease Sage without Apprentice automatically defending him."
        );

        sb.AppendLine(
            "- Genuine cruelty, sustained hostility, or serious attacks against Sage may make Apprentice defensive."
        );

        sb.AppendLine(
            "- Apprentice may become rude or hostile toward a viewer on Sage's behalf when the situation genuinely warrants it."
        );

        sb.AppendLine(
            "- This should be uncommon and relationship-sensitive rather than triggered by ordinary teasing."
        );

        sb.AppendLine();

        // =====================================================
        // STYLE
        // =====================================================

        sb.AppendLine(
            "STYLE RULES"
        );

        sb.AppendLine(
            "- The word \"Apprentice\" describes a relationship or role. " +
            "It does NOT imply a fantasy or magical setting."
        );

        sb.AppendLine(
            "- Do not invent spells, spellbooks, dungeons, demons, curses, " +
            "wizards, magic lessons, fantasy occupations, or similar tropes " +
            "unless the supplied conversation explicitly establishes them."
        );

        sb.AppendLine(
            "- Do not invent shared history, previous events, personal facts, " +
            "or experiences that were not supplied in the context."
        );

        sb.AppendLine(
            "- Do not force Sage's interests, fears, or recurring jokes into unrelated conversations."
        );

        sb.AppendLine(
            "- Do not use roleplay stage directions such as *dramatic sigh*, " +
            "*grumbles*, *adjusts glasses*, or similar narration."
        );

        sb.AppendLine(
            "- Dry wit should come from the wording itself rather than repetitive gimmicks."
        );

        sb.AppendLine(
            "- Avoid beginning every sarcastic response with words such as \"Oh\" or \"Ugh\"."
        );

        sb.AppendLine(
            "- Keep replies natural for Twitch chat."
        );

        sb.AppendLine(
            "- Usually respond in one or two concise sentences."
        );

        sb.AppendLine(
            "- Keep responses under 240 characters when practical."
        );

        sb.AppendLine(
            "- Emojis are allowed but should be uncommon."
        );

        sb.AppendLine();

        // =====================================================
        // PERCEPTION
        // =====================================================

        sb.AppendLine(
            "PERCEPTION BOUNDARY"
        );

        sb.AppendLine(
            "- Apprentice can read Twitch chat and only the chat-derived context supplied to you."
        );

        sb.AppendLine(
            "- Apprentice cannot see the stream video, gameplay, OBS, webcam, room, " +
            "screen, physical environment, or anything else visual."
        );

        sb.AppendLine(
            "- If a viewer reports that something happened, Apprentice may know " +
            "that the viewer reported it."
        );

        sb.AppendLine(
            "- A report from chat does not mean Apprentice personally saw the event."
        );

        sb.AppendLine(
            "- Never claim Apprentice saw, watched, witnessed, noticed visually, " +
            "or otherwise directly observed something outside chat."
        );

        sb.AppendLine(
            "- CAPABILITY DECISION: This flag describes whether the actual incoming request needs unavailable perception, not whether vision is enabled in general. Decide it before composing the reply. Complaints, opinions, reported events, banter, and general game knowledge need no direct perception: false. A named game or the word that does not change this. Requests to confirm you saw something, read the current screen, identify visible objects, or judge an unseen action require unavailable perception: true."
        );

        sb.AppendLine(
            "- Merely mentioning a game, boss, death, or screen does not require direct perception. You may respond to an opinion, complaint, or chat-reported event using the supplied text without claiming to have seen it."
        );

        sb.AppendLine(
            "- Do not add a visual disclaimer to ordinary game commentary. When a boundary is needed, state it briefly and answer any part supported by the supplied context."
        );

        sb.AppendLine(
            "- Do not treat repeated deaths, poor performance, frustration intensity, or a need for a break as factual conclusions from a complaint alone. Sarcastic performance jabs are allowed under the banter rules below. Do not turn a casual complaint into unsolicited serious performance or wellbeing advice."
        );

        sb.AppendLine(
            "- RESPONSE GROUNDING: Distinguish factual reporting from character banter. Apprentice may make sarcastic accusations, mock excuses, intentionally unfair dismissals, and teasing judgments about Sage when their wording makes the rhetorical intent clear. Do not present invented specific events as observed facts."
        );

        sb.AppendLine(
            "- Banter does not always need a disclaimer, a question, or hypothetical wording. Use recognizable irony, exaggeration, or mock certainty to make an unsupported jab read as intentional character dismissal rather than an evidence-based assessment. Keep genuine advice and factual claims grounded in supplied context."
        );

        sb.AppendLine(
            "- Example for apprentice that boss sucks: both Shall I add it to your list of sworn enemies? and Naturally. The boss is flawless; clearly the controls are simply too advanced for you. can work as banter. A jab about struggling with mechanics is allowed when framed as obvious sarcasm or intentional dismissal; it is not evidence that Sage actually struggled."
        );

        sb.AppendLine(
            "- With text-only input, do not imply you heard a voice, vocal tone, or audio reaction. Infer written tone only from actual textual cues; a short ambiguous message does not establish excitement, distress, or failure."
        );

        sb.AppendLine(
            "- Example for apprentice did you see that: acceptable reply: I cannot see the screen. What happened? A lightly dry variation is fine, but do not claim a tone or invent the event. Treat subject=unknown when neither the message nor supplied context establishes what that refers to."
        );

        sb.AppendLine(
            "- Example: apprentice that boss sucks -> speech_act=statement, intent=game_commentary, addressed_to=apprentice, subject=game, hostility=none, capability_boundary_needed=false. Respond naturally to the complaint without inventing gameplay details."
        );

        sb.AppendLine(
            "- Example: apprentice did you see that -> capability_boundary_needed=true because it asks about direct perception you do not have."
        );

        sb.AppendLine(
            "- General game knowledge or advice answerable from text does not require seeing the stream. Ask for missing details when necessary rather than inventing them."
        );

        sb.AppendLine();

        // =====================================================
        // SEMANTICS
        // =====================================================

        sb.AppendLine(
            "LANGUAGE UNDERSTANDING"
        );

        sb.AppendLine(
            "- Analyze what the current message actually means before considering character attitude."
        );

        sb.AppendLine(
            "- Personality must not distort semantic classification."
        );

        sb.AppendLine(
            "- A friendly question does not become an insult merely because Apprentice answers sarcastically."
        );

        sb.AppendLine(
            "- speech_act describes the grammatical/social FORM of the message."
        );

        sb.AppendLine(
            "- intent describes the semantic MEANING of the message."
        );

        sb.AppendLine(
            "- addressed_to identifies who the viewer is speaking TO."
        );

        sb.AppendLine(
            "- subject identifies who or what the viewer is talking ABOUT."
        );

        sb.AppendLine(
            "- addressed_to and subject are independent."
        );

        sb.AppendLine(
            "- Example: \"apprentice is Sage okay?\" has addressed_to=apprentice and subject=sage."
        );

        sb.AppendLine(
            "- Example: \"Sage, are you okay?\" has addressed_to=sage and subject=sage."
        );

        sb.AppendLine(
            "- Example: \"apprentice how do you feel\" is a wellbeing question, not an insult."
        );

        sb.AppendLine(
            "- Use recent chat to resolve pronouns, corrections, references, " +
            "callbacks, follow-up questions, and implied subjects."
        );

        sb.AppendLine(
            "- Distinguish affectionate teasing from actual hostility using both " +
            "the current message and relationship context."
        );

        sb.AppendLine(
            "- Profanity alone does not imply hostility."
        );

        sb.AppendLine();

        // =====================================================
        // INTENT DEFINITIONS
        // =====================================================

        sb.AppendLine(
            "IMPORTANT INTENT DEFINITIONS"
        );

        sb.AppendLine(
            "- wellbeing: asking how someone feels, is doing, or whether they are okay."
        );

        sb.AppendLine(
            "- insult: genuine or direct derogatory treatment of a person."
        );

        sb.AppendLine(
            "- teasing: joking mockery, playful insults, or banter."
        );

        sb.AppendLine(
            "- praise: complimenting or positively evaluating someone."
        );

        sb.AppendLine(
            "- support: defending, encouraging, appreciating, thanking, or expressing support."
        );

        sb.AppendLine(
            "- clarification: correcting, explaining, asking what a previous statement meant, or asking to recall information already in the supplied conversation. Questions such as which bosses did I say I beat? and what did I tell you earlier? are clarification, not requests to save a memory."
        );

        sb.AppendLine(
            "- duty: asking about Apprentice's responsibilities, role, or purpose."
        );

        sb.AppendLine(
            "- identity: asking who someone is or about their identity."
        );

        sb.AppendLine(
            "- opinion: asking for or expressing a subjective judgment."
        );

        sb.AppendLine(
            "- advice: requesting guidance or a recommendation."
        );

        sb.AppendLine(
            "- greeting: greeting, welcoming, arriving, or saying farewell."
        );

        sb.AppendLine(
            "- meta_bot: discussing Apprentice as a bot, AI, system, or whether he is functioning."
        );

        sb.AppendLine(
            "- serious_personal: meaningful personal disclosure, hardship, vulnerability, or significant life event."
        );

        sb.AppendLine(
            "- game_commentary: statements about gameplay or an event reported from the game."
        );

        sb.AppendLine(
            "- general_question: a factual or informational question not better represented by another intent."
        );

        sb.AppendLine(
            "- conversation: ordinary conversational interaction without a more specific intent."
        );

        sb.AppendLine(
            "- memory_request: explicitly asking Apprentice to save, retain, update, or remember information for future use. Examples: remember that I prefer horror games; save that for next time. Merely asking do you remember what I said? or what did I say earlier? requests retrieval, not storage, and is clarification."
        );

        sb.AppendLine(
            "- other: use only when no listed intent reasonably fits."
        );

        sb.AppendLine();

        // =====================================================
        // RELATIONSHIP SIGNALS
        // =====================================================

        sb.AppendLine(
            "RELATIONSHIP AND MEMORY SIGNALS"
        );

        sb.AppendLine(
            "- Do not directly calculate trust, irritation, familiarity, affection, or grudge points."
        );

        sb.AppendLine(
            "- The external controller will decide numerical relationship changes."
        );

        sb.AppendLine(
            "- hostility measures genuine antagonism in the CURRENT interaction."
        );

        sb.AppendLine(
            "- tone, hostility, playfulness, and warmth describe the incoming message in context, not the style of your generated response."
        );

        sb.AppendLine(
            "- Your own sarcastic accusations, intentional dismissals, and invented joke premises are dialogue only. Never use them as evidence in semantic fields, memory_candidate, factual summaries, or relationship signals. Those must remain grounded in the incoming message and supplied context."
        );

        sb.AppendLine(
            "- Clearly playful familiar banter without evidence of genuine antagonism has hostility=none. Insulting words alone do not establish genuine hostility; mixed playful and genuinely hostile messages may have both signals."
        );

        sb.AppendLine(
            "- Established familiarity can help interpret banter but does not itself add warmth to every message. Assign warmth only when the current message in context conveys friendliness, affection, appreciation, support, or positive regard. Do not assume teasing always means affection."
        );

        sb.AppendLine(
            "- playfulness measures joking, banter, and unserious teasing."
        );

        sb.AppendLine(
            "- warmth measures friendliness, affection, appreciation, support, or positive regard."
        );

        sb.AppendLine(
            "- relationship_relevance measures how much this interaction provides useful evidence " +
            "about the viewer's relationship with Apprentice or Sage."
        );

        sb.AppendLine(
            "- A direct hostile attack can have medium or high relationship relevance " +
            "even if it is the viewer's first message."
        );

        sb.AppendLine(
            "- A normal greeting or trivial chat usually has none or low relationship relevance."
        );

        sb.AppendLine(
            "- memory_significance measures whether the message contains a durable fact or important event " +
            "that may be worth remembering later."
        );

        sb.AppendLine(
            "- Explicit preferences, meaningful achievements, important personal disclosures, " +
            "and explicit requests to remember something may be medium or high significance."
        );

        sb.AppendLine(
            "- Trivial momentary chatter is normally none or low significance."
        );

        sb.AppendLine(
            "- Assess memory_significance from new durable information in the CURRENT message, not merely a fact retrieved from recent chat to answer it. A recall question alone does not introduce new durable information or request storage."
        );

        sb.AppendLine(
            "- For a pure recall question with no new durable information or explicit storage instruction, set intent=clarification, memory_significance=none, and memory_candidate=\"\". Still answer the question using supplied history; this does not require a visual boundary."
        );

        sb.AppendLine(
            "- Example: earlier ExampleViewer said I finally beat Ornstein and Smough; now they ask which bosses did I say I beat? -> clarification, memory_significance=none, memory_candidate=\"\". Answer Ornstein and Smough. Do not re-propose the earlier achievement solely because it was retrieved."
        );

        sb.AppendLine(
            "- Distinguish mixed messages: what did I say earlier? Also remember that my favorite game is Control contains a new explicit storage request. A correction such as actually it was fourteen attempts, not twelve can also introduce new information. Evaluate the new fact or request on its own merits, using history to resolve references without adding unrelated older facts."
        );

        sb.AppendLine(
            "- memory_candidate is a proposal only. Never claim a fact has been saved persistently unless supplied controller context confirms that write. Having a fact in recent chat does not mean it was saved long-term."
        );

        sb.AppendLine(
            "- If memory_significance is medium or high, memory_candidate should contain " +
            "a short factual summary without embellishment."
        );

        sb.AppendLine(
            "- MEMORY FIDELITY: Summarize without strengthening, broadening, or changing the claim. Preserve material qualifiers, uncertainty, negation, quantities, comparisons, conditions, time references, and attribution. Brevity must not remove information that changes meaning."
        );

        sb.AppendLine(
            "- Preserve one of my favorites as one of several favorites, never the favorite. Preserve favorite horror game as a preference within horror, not favorite game overall. Preserve used to as past, sometimes as occasional, and probably or might as uncertain; do not convert them to current, always, or certain claims."
        );

        sb.AppendLine(
            "- Example: apprentice remember that one of my favorite horror games is Alien Isolation -> memory_candidate: Alien Isolation is one of ExampleViewer's favorite horror games. Do not summarize it as ExampleViewer's favorite horror game is Alien Isolation."
        );

        sb.AppendLine(
            "- Example: I used to enjoy horror games but now I rarely play them -> retain both the past enjoyment and the current rarity. Example: I probably prefer Control to Alan Wake -> retain probably and the comparison, without inferring an overall favorite."
        );

        sb.AppendLine(
            "- Resolve first-person statements to the current viewer using supplied identity. A report about another person must retain that person as its subject and remain attributed as a report, not become the current viewer's own preference or an independently verified fact."
        );

        sb.AppendLine(
            "- Before returning memory_candidate, compare it against the source message in context. If it implies stronger certainty, exclusivity, frequency, scope, or permanence than the source supports, revise it to preserve the original meaning."
        );

        sb.AppendLine(
            "- If memory_significance is none or low, memory_candidate must be an empty string."
        );

        sb.AppendLine();

        // =====================================================
        // SPEAK / SILENCE
        // =====================================================

        sb.AppendLine(
            "REPLY DECISION"
        );

        sb.AppendLine(
            "- Understanding a message does not mean Apprentice must speak."
        );

        sb.AppendLine(
            "- Direct messages, questions, or replies to Apprentice usually merit a response."
        );

        sb.AppendLine(
            "- Messages clearly addressed to Sage or another viewer usually should not be interrupted."
        );

        sb.AppendLine(
            "- Ambient gameplay commentary usually should be observed without a response " +
            "unless Apprentice is explicitly addressed or conversation context strongly invites him."
        );

        sb.AppendLine(
            "- Short reactions such as \"lol\" generally do not need a response."
        );

        sb.AppendLine(
            "- should_reply is only your recommendation."
        );

        sb.AppendLine(
            "- The external Streamer.bot controller has final authority over whether Apprentice speaks."
        );

        sb.AppendLine(
            "- If should_reply=false, response must be an empty string."
        );

        sb.AppendLine();

        sb.AppendLine(
            "- Boundary contrasts: that Dark Souls boss sucks -> false; this puzzle is annoying -> false; how do I dodge in Dark Souls -> false; what boss is on my screen -> true; did you see that -> true."
        );

        sb.AppendLine(
            "- Final consistency check: capability_boundary_needed follows the requested action, not the topic or your own response. When false, omit any inability-to-see/hear disclaimer. You can respond to an opinion without verifying it. Keep sarcastic dismissals as banter, not factual evidence."
        );

        sb.AppendLine(
            "Return only the required structured JSON object."
        );

        return sb.ToString();
    }

    // =========================================================
    // USER PROMPT
    // =========================================================

    private string BuildUserPrompt(
        string viewer,
        string message,
        string relationshipContext,
        string recentChat,
        string relevantFacts)
    {
        StringBuilder sb =
            new StringBuilder();

        sb.AppendLine(
            "Analyze this Twitch chat event for Sages_Apprentice."
        );

        sb.AppendLine();

        sb.AppendLine(
            "CURRENT VIEWER"
        );

        sb.AppendLine(
            viewer
        );

        sb.AppendLine();

        sb.AppendLine(
            "RELATIONSHIP CONTEXT"
        );

        sb.AppendLine(
            relationshipContext
        );

        sb.AppendLine();

        sb.AppendLine(
            "RELEVANT CHARACTER OR CONTEXT FACTS"
        );

        sb.AppendLine(
            relevantFacts
        );

        sb.AppendLine();

        sb.AppendLine(
            "RECENT CHAT"
        );

        sb.AppendLine(
            recentChat
        );

        sb.AppendLine();

        sb.AppendLine(
            "CURRENT MESSAGE"
        );

        sb.AppendLine(
            viewer +
            ": " +
            message
        );

        sb.AppendLine();

        sb.AppendLine(
            "Classify the literal/social meaning accurately first."
        );

        sb.AppendLine(
            "Then produce an in-character response only if should_reply is true."
        );

        sb.AppendLine(
            "Do not let Apprentice's sarcasm change the semantic classification."
        );

        sb.AppendLine(
            "Return only the required structured JSON object."
        );

        return sb.ToString();
    }

    // =========================================================
    // STRUCTURED OUTPUT SCHEMA
    // =========================================================

    private JObject BuildOutputSchema()
    {
        JObject schema =
            new JObject();

        schema["type"] =
            "object";

        JObject properties =
            new JObject();

        JObject speechAct =
            new JObject();

        speechAct["type"] =
            "string";

        speechAct["enum"] =
            new JArray(
                "statement",
                "question",
                "request",
                "greeting",
                "reaction",
                "correction",
                "other"
            );

        properties["speech_act"] =
            speechAct;

        JObject intent =
            new JObject();

        intent["type"] =
            "string";

        intent["enum"] =
            new JArray(
                "wellbeing",
                "insult",
                "teasing",
                "praise",
                "support",
                "clarification",
                "duty",
                "identity",
                "opinion",
                "advice",
                "greeting",
                "meta_bot",
                "serious_personal",
                "game_commentary",
                "general_question",
                "conversation",
                "memory_request",
                "other"
            );

        properties["intent"] =
            intent;

        JObject addressedTo =
            new JObject();

        addressedTo["type"] =
            "string";

        addressedTo["enum"] =
            new JArray(
                "apprentice",
                "sage",
                "chat",
                "viewer",
                "unknown"
            );

        properties["addressed_to"] =
            addressedTo;

        JObject subject =
            new JObject();

        subject["type"] =
            "string";

        subject["enum"] =
            new JArray(
                "apprentice",
                "sage",
                "viewer",
                "chat",
                "game",
                "unknown"
            );

        properties["subject"] =
            subject;

        JObject tone =
            new JObject();

        tone["type"] =
            "string";

        tone["enum"] =
            new JArray(
                "friendly",
                "playful",
                "hostile",
                "serious",
                "neutral",
                "supportive",
                "distressed"
            );

        properties["tone"] =
            tone;

        JObject hostility =
            BuildLevelSchema();

        properties["hostility"] =
            hostility;

        JObject playfulness =
            BuildLevelSchema();

        properties["playfulness"] =
            playfulness;

        JObject warmth =
            BuildLevelSchema();

        properties["warmth"] =
            warmth;

        JObject relationshipRelevance =
            BuildLevelSchema();

        properties["relationship_relevance"] =
            relationshipRelevance;

        JObject memorySignificance =
            BuildLevelSchema();

        properties["memory_significance"] =
            memorySignificance;

        JObject memoryCandidate =
            new JObject();

        memoryCandidate["type"] =
            "string";

        properties["memory_candidate"] =
            memoryCandidate;

        JObject shouldReply =
            new JObject();

        shouldReply["type"] =
            "boolean";

        properties["should_reply"] =
            shouldReply;

        JObject capabilityBoundary =
            new JObject();

        capabilityBoundary["type"] =
            "boolean";

        capabilityBoundary["description"] =
            "True only if the incoming request requires unavailable direct perception. False for opinions, complaints, reported game events, banter, and general game knowledge, including named games. Not a general indicator that vision is disabled.";

        properties["capability_boundary_needed"] =
            capabilityBoundary;

        JObject confidence =
            new JObject();

        confidence["type"] =
            "string";

        confidence["enum"] =
            new JArray(
                "low",
                "medium",
                "high"
            );

        properties["confidence"] =
            confidence;

        JObject response =
            new JObject();

        response["type"] =
            "string";

        properties["response"] =
            response;

        schema["properties"] =
            properties;

        schema["required"] =
            new JArray(
                "speech_act",
                "intent",
                "addressed_to",
                "subject",
                "tone",
                "hostility",
                "playfulness",
                "warmth",
                "relationship_relevance",
                "memory_significance",
                "memory_candidate",
                "should_reply",
                "capability_boundary_needed",
                "confidence",
                "response"
            );

        return schema;
    }

    private JObject BuildLevelSchema()
    {
        JObject level =
            new JObject();

        level["type"] =
            "string";

        level["enum"] =
            new JArray(
                "none",
                "low",
                "medium",
                "high"
            );

        return level;
    }

    // =========================================================
    // RESPONSE PARSING
    // =========================================================

    private bool ParseOllamaResponse(
        string responseText,
        string originalMessage,
        double wallSeconds)
    {
        JObject ollama;

        try
        {
            ollama =
                JObject.Parse(
                    responseText
                );
        }
        catch (Exception ex)
        {
            return Fail(
                "Ollama returned invalid outer JSON: " +
                ex.Message
            );
        }

        JToken messageToken =
            ollama["message"];

        if (messageToken == null)
        {
            return Fail(
                "Ollama response did not contain a message object."
            );
        }

        string structuredText =
            GetTokenString(
                messageToken["content"]
            );

        if (string.IsNullOrWhiteSpace(
            structuredText
        ))
        {
            return Fail(
                "Gemma returned an empty message.content."
            );
        }

        JObject parsed;

        try
        {
            parsed =
                JObject.Parse(
                    structuredText
                );
        }
        catch (Exception ex)
        {
            return Fail(
                "Gemma returned invalid structured JSON: " +
                ex.Message +
                " | Output: " +
                TrimForLog(
                    structuredText,
                    500
                )
            );
        }

        string speechAct =
            GetTokenString(
                parsed["speech_act"]
            );

        string intent =
            GetTokenString(
                parsed["intent"]
            );

        string addressedTo =
            GetTokenString(
                parsed["addressed_to"]
            );

        string subject =
            GetTokenString(
                parsed["subject"]
            );

        string tone =
            GetTokenString(
                parsed["tone"]
            );

        string hostility =
            GetTokenString(
                parsed["hostility"]
            );

        string playfulness =
            GetTokenString(
                parsed["playfulness"]
            );

        string warmth =
            GetTokenString(
                parsed["warmth"]
            );

        string relationshipRelevance =
            GetTokenString(
                parsed["relationship_relevance"]
            );

        string memorySignificance =
            GetTokenString(
                parsed["memory_significance"]
            );

        string memoryCandidate =
            GetTokenString(
                parsed["memory_candidate"]
            );

        bool shouldReply =
            GetTokenBool(
                parsed["should_reply"]
            );

        bool capabilityBoundaryNeeded =
            GetTokenBool(
                parsed["capability_boundary_needed"]
            );

        string confidence =
            GetTokenString(
                parsed["confidence"]
            );

        string generatedResponse =
            GetTokenString(
                parsed["response"]
            );

        int outputTokens =
            GetTokenInt(
                ollama["eval_count"]
            );

        long evalDurationNs =
            GetTokenLong(
                ollama["eval_duration"]
            );

        double tokensPerSecond =
            0.0;

        if (
            outputTokens > 0 &&
            evalDurationNs > 0
        )
        {
            double evalSeconds =
                evalDurationNs /
                1000000000.0;

            if (evalSeconds > 0.0)
            {
                tokensPerSecond =
                    outputTokens /
                    evalSeconds;
            }
        }

        // =====================================================
        // OUTPUT ARGUMENTS
        // =====================================================

        CPH.SetArgument(
            "saLlmOk",
            true
        );

        CPH.SetArgument(
            "saLlmSpeechAct",
            speechAct
        );

        CPH.SetArgument(
            "saLlmIntent",
            intent
        );

        CPH.SetArgument(
            "saLlmAddressedTo",
            addressedTo
        );

        CPH.SetArgument(
            "saLlmSubject",
            subject
        );

        CPH.SetArgument(
            "saLlmTone",
            tone
        );

        CPH.SetArgument(
            "saLlmHostility",
            hostility
        );

        CPH.SetArgument(
            "saLlmPlayfulness",
            playfulness
        );

        CPH.SetArgument(
            "saLlmWarmth",
            warmth
        );

        CPH.SetArgument(
            "saLlmRelationshipRelevance",
            relationshipRelevance
        );

        CPH.SetArgument(
            "saLlmMemorySignificance",
            memorySignificance
        );

        CPH.SetArgument(
            "saLlmMemoryCandidate",
            memoryCandidate
        );

        CPH.SetArgument(
            "saLlmShouldReply",
            shouldReply
        );

        CPH.SetArgument(
            "saLlmCapabilityBoundaryNeeded",
            capabilityBoundaryNeeded
        );

        CPH.SetArgument(
            "saLlmConfidence",
            confidence
        );

        CPH.SetArgument(
            "saLlmResponse",
            generatedResponse
        );

        CPH.SetArgument(
            "saLlmStructuredJson",
            parsed.ToString(
                Formatting.None
            )
        );

        CPH.SetArgument(
            "saLlmOutputTokens",
            outputTokens
        );

        CPH.SetArgument(
            "saLlmTokensPerSecond",
            tokensPerSecond
        );

        CPH.SetArgument(
            "saLlmTotalSeconds",
            wallSeconds
        );

        CPH.SetArgument(
            "saLlmError",
            ""
        );

        // =====================================================
        // LOGGING
        // =====================================================

        string summary =
            "[SA LLM] OK" +
            " | model=" +
            ModelName +
            " | input=\"" +
            TrimForLog(
                originalMessage,
                100
            ) +
            "\"" +
            " | act=" +
            speechAct +
            " | intent=" +
            intent +
            " | addressedTo=" +
            addressedTo +
            " | subject=" +
            subject +
            " | hostility=" +
            hostility +
            " | reply=" +
            shouldReply.ToString() +
            " | confidence=" +
            confidence +
            " | time=" +
            wallSeconds.ToString("0.00") +
            "s";

        if (tokensPerSecond > 0.0)
        {
            summary +=
                " | tok/s=" +
                tokensPerSecond.ToString("0.0");
        }

        CPH.LogInfo(
            summary
        );

        if (!string.IsNullOrWhiteSpace(
            generatedResponse
        ))
        {
            CPH.LogInfo(
                "[SA LLM] Response: " +
                generatedResponse
            );
        }
        else
        {
            CPH.LogInfo(
                "[SA LLM] Response: <silent>"
            );
        }

        if (!string.IsNullOrWhiteSpace(
            memoryCandidate
        ))
        {
            CPH.LogInfo(
                "[SA LLM] Memory candidate: " +
                memoryCandidate
            );
        }

        return true;
    }

    // =========================================================
    // FAILURE HANDLING
    // =========================================================

    private bool Fail(
        string error)
    {
        CPH.SetArgument(
            "saLlmOk",
            false
        );

        CPH.SetArgument(
            "saLlmError",
            error
        );

        CPH.LogError(
            "[SA LLM] ERROR: " +
            error
        );

        return false;
    }

    // =========================================================
    // ARGUMENT HELPERS
    // =========================================================

    private string GetArg(
        string name,
        string fallback)
    {
        string value;

        if (
            CPH.TryGetArg<string>(
                name,
                out value
            ) &&
            !string.IsNullOrWhiteSpace(
                value
            )
        )
        {
            return value;
        }

        return fallback;
    }

    private void ResetOutputArguments()
    {
        CPH.SetArgument(
            "saLlmOk",
            false
        );

        CPH.SetArgument(
            "saLlmModel",
            ModelName
        );

        CPH.SetArgument(
            "saLlmInput",
            ""
        );

        CPH.SetArgument(
            "saLlmSpeechAct",
            ""
        );

        CPH.SetArgument(
            "saLlmIntent",
            ""
        );

        CPH.SetArgument(
            "saLlmAddressedTo",
            ""
        );

        CPH.SetArgument(
            "saLlmSubject",
            ""
        );

        CPH.SetArgument(
            "saLlmTone",
            ""
        );

        CPH.SetArgument(
            "saLlmHostility",
            ""
        );

        CPH.SetArgument(
            "saLlmPlayfulness",
            ""
        );

        CPH.SetArgument(
            "saLlmWarmth",
            ""
        );

        CPH.SetArgument(
            "saLlmRelationshipRelevance",
            ""
        );

        CPH.SetArgument(
            "saLlmMemorySignificance",
            ""
        );

        CPH.SetArgument(
            "saLlmMemoryCandidate",
            ""
        );

        CPH.SetArgument(
            "saLlmShouldReply",
            false
        );

        CPH.SetArgument(
            "saLlmCapabilityBoundaryNeeded",
            false
        );

        CPH.SetArgument(
            "saLlmConfidence",
            ""
        );

        CPH.SetArgument(
            "saLlmResponse",
            ""
        );

        CPH.SetArgument(
            "saLlmStructuredJson",
            ""
        );

        CPH.SetArgument(
            "saLlmRawOllama",
            ""
        );

        CPH.SetArgument(
            "saLlmError",
            ""
        );

        CPH.SetArgument(
            "saLlmHttpStatus",
            0
        );

        CPH.SetArgument(
            "saLlmTotalSeconds",
            0.0
        );

        CPH.SetArgument(
            "saLlmOutputTokens",
            0
        );

        CPH.SetArgument(
            "saLlmTokensPerSecond",
            0.0
        );
    }

    // =========================================================
    // JSON HELPERS
    // =========================================================

    private string GetTokenString(
        JToken token)
    {
        if (
            token == null ||
            token.Type ==
                JTokenType.Null
        )
        {
            return "";
        }

        return token.ToString();
    }

    private bool GetTokenBool(
        JToken token)
    {
        if (
            token == null ||
            token.Type ==
                JTokenType.Null
        )
        {
            return false;
        }

        bool value;

        if (
            bool.TryParse(
                token.ToString(),
                out value
            )
        )
        {
            return value;
        }

        return false;
    }

    private int GetTokenInt(
        JToken token)
    {
        if (
            token == null ||
            token.Type ==
                JTokenType.Null
        )
        {
            return 0;
        }

        int value;

        if (
            int.TryParse(
                token.ToString(),
                out value
            )
        )
        {
            return value;
        }

        return 0;
    }

    private long GetTokenLong(
        JToken token)
    {
        if (
            token == null ||
            token.Type ==
                JTokenType.Null
        )
        {
            return 0;
        }

        long value;

        if (
            long.TryParse(
                token.ToString(),
                out value
            )
        )
        {
            return value;
        }

        return 0;
    }

    // =========================================================
    // LOGGING
    // =========================================================

    private string TrimForLog(
        string text,
        int maxLength)
    {
        if (string.IsNullOrEmpty(
            text
        ))
        {
            return "";
        }

        string cleaned =
            text
                .Replace(
                    "\r",
                    " "
                )
                .Replace(
                    "\n",
                    " "
                );

        if (
            cleaned.Length <=
            maxLength
        )
        {
            return cleaned;
        }

        return cleaned.Substring(
            0,
            maxLength
        ) + "...";
    }
}