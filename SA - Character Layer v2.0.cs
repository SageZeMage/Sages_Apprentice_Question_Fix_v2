using System;

public class CPHInline
{
    // Sages_Apprentice Character Layer v2.0
    //
    // This layer decides WHAT kind of character response is appropriate.
    // It does not decide whether Apprentice definitely speaks; Attention Engine
    // still makes that decision.
    //
    // Apprentice only knows Twitch chat and chat-derived state. He does not see
    // gameplay, OBS, the stream video, or off-chat events.

    private const string SageUserId = "69114580";

    public bool Execute()
    {
        string userId = Read("userId");
        string text = Read("saNormalized");
        if (string.IsNullOrWhiteSpace(text))
            text = Read("rawInput");
        if (string.IsNullOrWhiteSpace(text))
            text = Read("message");

        text = Normalize(text);

        string target = Read("saEffectiveTarget").ToLowerInvariant();
        string behavior = Read("saBehaviorEvent").ToLowerInvariant();
        string intent = Read("saIntent").ToLowerInvariant();
        string pattern = Read("saContextPattern").ToLowerInvariant();

        int hostility = ReadInt("saHostility");
        int playfulness = ReadInt("saPlayfulness");

        bool isSage = userId == SageUserId || ReadBool("saIsBroadcaster");
        bool isQuestion = ReadBool("saIsQuestion");
        bool continuationCandidate = ReadBool("saContinuationCandidate");
        bool recentApprenticeConversation = ReadBool("saRecentApprenticeConversation");

        bool addressesApprentice =
            target == "apprentice" ||
            Contains(text, "apprentice") ||
            Contains(text, "sage apprentice") ||
            Contains(text, "sages apprentice");

        string lastResponseUser = CPH.GetGlobalVar<string>("SA_ATT_LAST_RESPONSE_USER", false) ?? "";
        bool sameUserAsLastResponse =
            !string.IsNullOrWhiteSpace(lastResponseUser) &&
            !string.IsNullOrWhiteSpace(Read("user")) &&
            string.Equals(lastResponseUser, Read("user"), StringComparison.OrdinalIgnoreCase);

        bool hasContinuationEvidence =
            pattern == "conversation_continuation" ||
            continuationCandidate ||
            recentApprenticeConversation ||
            sameUserAsLastResponse;

        string responseClass = "";
        string semanticKind = "none";
        string motivation = isSage ? "familiar_loyalty" : "guarded_observation";
        int attentionBoost = 0;

        // Specific semantic meanings always beat the generic Sage-question path.
        if (addressesApprentice && IsMalfunctionReport(text))
        {
            responseClass = "apprentice_malfunction";
            semanticKind = "malfunction_report";
            motivation = "dry_self_diagnosis";
            attentionBoost = 32;
        }
        else if (addressesApprentice && IsWellbeingQuestion(text))
        {
            responseClass = "character_wellbeing";
            semanticKind = "character_wellbeing";
            motivation = isSage ? "familiar_vulnerability" : "guarded_warmth";
            attentionBoost = isSage ? 40 : 28;
        }
        else if (addressesApprentice && IsDutyQuestion(text))
        {
            responseClass = "character_duty";
            semanticKind = "character_duty";
            motivation = isSage ? "loyal_professionalism" : "dry_self_definition";
            attentionBoost = isSage ? 36 : 24;
        }
        else if (addressesApprentice && hasContinuationEvidence && IsClarificationFollowup(text))
        {
            responseClass = "clarification_followup";
            semanticKind = "clarification_followup";
            motivation = isSage ? "reluctant_correction" : "controlled_clarification";
            attentionBoost = isSage ? 34 : 24;
        }
        // Genuine hostility toward Sage gets a loyal response. Ordinary teasing
        // does not automatically trigger a defense.
        else if (!isSage &&
                 target == "sage" &&
                 (behavior == "hostile_insult" || intent == "insult" || hostility >= 70) &&
                 playfulness < 70)
        {
            responseClass = "sage_defend";
            semanticKind = "sage_defense";
            motivation = "loyalty_over_sarcasm";
            attentionBoost = 58;
        }
        else if (isSage && IsDistress(text))
        {
            responseClass = "sage_encourage";
            semanticKind = "sage_distress";
            motivation = "quiet_loyalty";
            attentionBoost = 62;
        }
        else if (isSage && IsReportedSuccess(text))
        {
            responseClass = "sage_grudging_praise";
            semanticKind = "sage_success";
            motivation = "reluctant_pride";
            attentionBoost = 62;
        }
        else if (isSage &&
                 addressesApprentice &&
                 (intent == "greeting" || behavior == "greeting"))
        {
            responseClass = "sage_greeting";
            semanticKind = "sage_greeting";
            motivation = "familiar_exasperation";
            attentionBoost = 32;
        }
        else if (isSage && addressesApprentice && isQuestion)
        {
            responseClass = "sage_question";
            semanticKind = "generic_sage_question";
            motivation = "reluctant_service";
            attentionBoost = 18;
        }

        CPH.SetArgument("saCharacterResponseClass", responseClass);
        CPH.SetArgument("saCharacterSemanticKind", semanticKind);
        CPH.SetArgument("saCharacterMotivation", motivation);
        CPH.SetArgument("saCharacterEvidence", "chat_text_only");
        CPH.SetArgument("saCharacterAttentionBoost", attentionBoost);
        CPH.SetArgument("saCharacterIsSage", isSage);

        return true;
    }

    private bool IsWellbeingQuestion(string text)
    {
        if (Contains(text, "how do you feel about"))
            return false;

        return Contains(text, "how do you feel") ||
               Contains(text, "how are you feeling") ||
               Contains(text, "how are you") ||
               Contains(text, "how you doing") ||
               Contains(text, "are you okay") ||
               Contains(text, "are you ok") ||
               Contains(text, "you okay") ||
               Contains(text, "you ok") ||
               Contains(text, "you alright") ||
               Contains(text, "you good");
    }

    private bool IsDutyQuestion(string text)
    {
        return Contains(text, "what is your duty") ||
               Contains(text, "what's your duty") ||
               Contains(text, "whats your duty") ||
               Contains(text, "what are your duties") ||
               Contains(text, "what is your purpose") ||
               Contains(text, "what's your purpose") ||
               Contains(text, "whats your purpose") ||
               Contains(text, "what is your job") ||
               Contains(text, "what's your job") ||
               Contains(text, "whats your job") ||
               Contains(text, "what do you do") ||
               Contains(text, "what are you here for");
    }

    private bool IsClarificationFollowup(string text)
    {
        return text == "explain" ||
               text == "elaborate" ||
               text == "clarify" ||
               Contains(text, "explain that") ||
               Contains(text, "explain yourself") ||
               Contains(text, "what do you mean") ||
               Contains(text, "what did you mean") ||
               Contains(text, "what context do you need") ||
               Contains(text, "what context") ||
               Contains(text, "what do you need from me") ||
               Contains(text, "be more specific") ||
               Contains(text, "what needs explaining");
    }

    private bool IsMalfunctionReport(string text)
    {
        return Contains(text, "malfunctioning") ||
               Contains(text, "apprentice is broken") ||
               Contains(text, "apprentice is bugged") ||
               Contains(text, "bot is broken") ||
               Contains(text, "bot is bugged") ||
               Contains(text, "not working right") ||
               Contains(text, "something is wrong with") ||
               Contains(text, "your apprentice is broken") ||
               Contains(text, "your apprentice is malfunctioning");
    }

    private bool IsDistress(string text)
    {
        return Contains(text, "i'm frustrated") ||
               Contains(text, "i am frustrated") ||
               Contains(text, "i'm stressed") ||
               Contains(text, "i am stressed") ||
               Contains(text, "i'm exhausted") ||
               Contains(text, "i am exhausted") ||
               Contains(text, "i need a break") ||
               Contains(text, "i'm having a rough time") ||
               Contains(text, "i am having a rough time") ||
               Contains(text, "today has been rough") ||
               Contains(text, "i feel awful");
    }

    private bool IsReportedSuccess(string text)
    {
        return Contains(text, "i did it") ||
               Contains(text, "we did it") ||
               Contains(text, "i finally got it") ||
               Contains(text, "i figured it out") ||
               Contains(text, "i managed it") ||
               Contains(text, "i won") ||
               Contains(text, "we won");
    }

    private string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string text = value.ToLowerInvariant();
        text = text.Replace("’", "'");
        text = text.Replace(".", " ");
        text = text.Replace(",", " ");
        text = text.Replace("!", " ");
        text = text.Replace("?", " ");
        text = text.Replace(":", " ");
        text = text.Replace(";", " ");
        text = text.Replace("\n", " ");
        text = text.Replace("\r", " ");

        while (text.IndexOf("  ", StringComparison.Ordinal) >= 0)
            text = text.Replace("  ", " ");

        return text.Trim();
    }

    private bool Contains(string text, string phrase)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(phrase))
            return false;

        string a = " " + text.Trim() + " ";
        string b = " " + phrase.Trim() + " ";
        return a.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private string Read(string name)
    {
        string value = "";
        CPH.TryGetArg(name, out value);
        return value ?? "";
    }

    private int ReadInt(string name)
    {
        int value = 0;
        CPH.TryGetArg(name, out value);
        return value;
    }

    private bool ReadBool(string name)
    {
        bool value = false;
        CPH.TryGetArg(name, out value);
        return value;
    }
}
