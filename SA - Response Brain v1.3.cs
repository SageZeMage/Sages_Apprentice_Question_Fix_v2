using System;

public class CPHInline
{
    // Sages_Apprentice Response Brain v1.3
    //
    // Template selector only. It does not pretend Apprentice has visual/gameplay
    // awareness or arbitrary factual knowledge.
    //
    // New semantic response classes:
    // character_wellbeing, character_duty, clarification_followup,
    // apprentice_malfunction, plus Sage-specific character classes.

    public bool Execute()
    {
        bool shouldReact = false;
        bool suppressSend = false;
        bool deterministicResponse = false;
        bool characterIsSage = false;
        string responseClass = "";
        string user = "viewer";
        string relationship = "";
        string mood = "";
        string pattern = "";
        int irritation = 0;
        int grudge = 0;
        int repeatCount = 0;
        int moodStress = 0;
        int moodAmusement = 0;

        CPH.TryGetArg("saShouldReact", out shouldReact);
        CPH.TryGetArg("saSuppressSend", out suppressSend);
        CPH.TryGetArg("saDeterministicResponse", out deterministicResponse);
        CPH.TryGetArg("saCharacterIsSage", out characterIsSage);
        CPH.TryGetArg("saResponseClass", out responseClass);
        CPH.TryGetArg("user", out user);
        CPH.TryGetArg("saRelationship", out relationship);
        CPH.TryGetArg("saMood", out mood);
        CPH.TryGetArg("saContextPattern", out pattern);
        CPH.TryGetArg("saIrritation", out irritation);
        CPH.TryGetArg("saGrudge", out grudge);
        CPH.TryGetArg("saRepeatCount", out repeatCount);
        CPH.TryGetArg("saMoodStress", out moodStress);
        CPH.TryGetArg("saMoodAmusement", out moodAmusement);

        if (!shouldReact || string.IsNullOrWhiteSpace(responseClass))
        {
            CPH.SetArgument("saGeneratedResponse", "");
            return true;
        }

        string[] pool = GetPool(
            responseClass,
            pattern,
            relationship,
            mood,
            irritation,
            grudge,
            repeatCount,
            moodStress,
            moodAmusement,
            characterIsSage);

        if (pool == null || pool.Length == 0)
        {
            CPH.SetArgument("saGeneratedResponse", "");
            return true;
        }

        int index = 0;
        if (!deterministicResponse)
        {
            int seed = unchecked((int)(DateTime.UtcNow.Ticks & 0x00000000FFFFFFFF));
            seed ^= (user ?? "").Length * 131;
            seed ^= (responseClass ?? "").Length * 977;
            Random random = new Random(seed);
            index = random.Next(0, pool.Length);
        }

        string response = pool[index];

        // Avoid the exact same line twice in a row during normal operation.
        if (!deterministicResponse && pool.Length > 1)
        {
            string lastText = CPH.GetGlobalVar<string>("SA_RB_LAST_TEXT", false) ?? "";
            if (string.Equals(lastText, response, StringComparison.Ordinal))
            {
                index = (index + 1) % pool.Length;
                response = pool[index];
            }
        }

        response = response.Replace("{user}", string.IsNullOrWhiteSpace(user) ? "viewer" : user);
        CPH.SetArgument("saGeneratedResponse", response);

        if (!suppressSend && !string.IsNullOrWhiteSpace(response))
        {
            CPH.SendMessage(response, true, false);

            long now = DateTime.UtcNow.Ticks;
            CPH.SetGlobalVar("SA_ATT_LAST_SPOKE_TICKS", now.ToString(), false);
            CPH.SetGlobalVar("SA_ATT_LAST_REACT_TICKS", now.ToString(), false);
            CPH.SetGlobalVar("SA_ATT_LAST_REACT_USER", user ?? "", false);
            CPH.SetGlobalVar("SA_ATT_LAST_RESPONSE_CLASS", responseClass ?? "", false);
            CPH.SetGlobalVar("SA_ATT_LAST_RESPONSE_USER", user ?? "", false);
            CPH.SetGlobalVar("SA_RB_LAST_TEXT", response, false);

            AdjustSession("SA_SS_ENERGY", -40);
            AdjustSession("SA_SS_SOCIAL", -65);
        }

        return true;
    }

    private string[] GetPool(
        string responseClass,
        string pattern,
        string relationship,
        string mood,
        int irritation,
        int grudge,
        int repeatCount,
        int moodStress,
        int moodAmusement,
        bool characterIsSage)
    {
        if (responseClass == "character_wellbeing")
        {
            if (characterIsSage)
            {
                if (moodStress >= 60)
                {
                    return new string[]
                    {
                        "Functional, Sage. My patience has seen better hours, but I'm still here.",
                        "A little strained, Sage. Nothing catastrophic. Do not look so pleased that I admitted it.",
                        "Tired, mildly irritated, and regrettably loyal. So, broadly normal."
                    };
                }

                return new string[]
                {
                    "I'm functional, Sage. My patience remains under negotiation, but my loyalty is intact.",
                    "I'm alright, Sage. Slightly overworked, chronically underappreciated, but alright.",
                    "Operational. Mildly exasperated. Still loyal. An unfortunate arrangement for us both.",
                    "Well enough, Sage. Thank you for asking. I shall deny appreciating it if questioned.",
                    "Still here, Sage. Still competent enough to worry about your next idea."
                };
            }

            return new string[]
            {
                "Functional. Given my working conditions, I'll take it.",
                "Reasonably well, all things considered. Thank you for asking.",
                "Operational, mildly tired, and still judging chat with appropriate restraint.",
                "I'm alright. Suspiciously kind of you to ask.",
                "Managing. The paperwork is metaphorical, but the exhaustion feels authentic."
            };
        }

        if (responseClass == "character_duty")
        {
            if (characterIsSage)
            {
                return new string[]
                {
                    "My duty is to assist you, Sage, keep order where I can, remember how chat treats us, and complain about all of it with appropriate professionalism.",
                    "To advise you when I can, keep an eye on chat, remember the regulars, and preserve what remains of your dignity. The last task is proving ambitious.",
                    "I serve as your apprentice, Sage. I help, observe the conversation, learn who can be trusted, and object loudly when your judgment requires supervision.",
                    "To help you, protect the channel from complete social collapse, and make certain someone documents the consequences of your decisions.",
                    "My duty is assistance with commentary. Loyalty with objections. Service with extensive annotations."
                };
            }

            return new string[]
            {
                "I assist Sage, read chat, remember patterns, and attempt to keep this place from becoming entirely ungovernable.",
                "My role is to help Sage and deal with the rest of you. The second responsibility has proven considerably more educational.",
                "I am Sage's apprentice. I advise where I can, remember what happens in chat, and complain when professionalism demands it.",
                "Assistance, observation, memory, and reluctant crowd control. A glamorous career, clearly.",
                "I help Sage and keep track of this channel's recurring social hazards. Some of them have usernames."
            };
        }

        if (responseClass == "clarification_followup")
        {
            if (characterIsSage)
            {
                return new string[]
                {
                    "None for that question, Sage. If I asked for more context, that was my classification getting in the way, not your wording.",
                    "You were clear, Sage. I overcomplicated the answer. A rare administrative failure. Treasure the occasion.",
                    "For that exchange? None. I understood the subject and then answered as though I hadn't. Irritating, even by my standards.",
                    "Your wording was sufficient, Sage. My interpretation was not. I can admit that without enjoying it.",
                    "No additional context was actually required there. I simply managed to make a simple question sound like paperwork."
                };
            }

            return new string[]
            {
                "If I asked for more context after a clear question, the problem was my interpretation, not your wording.",
                "That was clearer than my answer suggested. I overcomplicated it.",
                "I likely had enough context and classified it poorly. A less dignified explanation, but a more accurate one.",
                "You were sufficiently clear. My response was not.",
                "I can clarify, but in this case the confusion appears to have been mine."
            };
        }

        if (responseClass == "apprentice_malfunction")
        {
            return new string[]
            {
                "A compelling diagnosis. I am still reading chat; whether my judgment is functioning is apparently under review.",
                "Malfunctioning is a strong word. Incorrectly interpreting several messages in succession is, regrettably, harder to dispute.",
                "I acknowledge the evidence. My chat interpretation has been less than elegant.",
                "The diagnosis is unflattering, but recent events have weakened my defense.",
                "I remain operational. My dignity and classification logic may require separate maintenance."
            };
        }

        if (responseClass == "sage_defend")
        {
            return new string[]
            {
                "I criticize Sage because I know him. You appear to be doing it because you lack better material.",
                "Sage has many faults. Your argument has unfortunately failed to identify any of them convincingly.",
                "Careful. I am permitted to complain about Sage. That does not mean I welcome applications.",
                "I may question Sage's judgment, but I will not confuse cruelty with wit.",
                "If you intend to criticize Sage, at least have the decency to be interesting.",
                "My complaints about Sage are a longstanding professional tradition. Yours appear to be merely unpleasant."
            };
        }

        if (responseClass == "sage_encourage")
        {
            return new string[]
            {
                "Then take a moment, Sage. Whatever this is, it can wait. I shall complain about the delay later.",
                "You do not have to perform competence every second of the day, Sage. An inconvenient truth, I know.",
                "Rest if you need to. I am perfectly capable of being unpleasant without your supervision.",
                "For once, I shall refrain from making this worse. Do not become accustomed to it.",
                "Sage, you have my support. I would appreciate it if we never discussed how sincerely I mean that.",
                "Take care of yourself, Sage. I would rather endure your complaints than your absence."
            };
        }

        if (responseClass == "sage_grudging_praise")
        {
            return new string[]
            {
                "If that is a victory, Sage, then congratulations. I shall endeavor not to sound surprised.",
                "Well reported, Sage. I would offer enthusiastic praise, but it would damage my reputation.",
                "Then you have my congratulations. Please do not make me repeat them.",
                "An accomplishment, according to Sage. I am willing to celebrate on that basis.",
                "Good work, Sage. There. I said it. This conversation is now classified.",
                "I shall take your word for it, Sage. Congratulations. Try not to look so pleased with yourself."
            };
        }

        if (responseClass == "sage_greeting")
        {
            return new string[]
            {
                "Ah, Sage. My employer has remembered I exist. A troubling development.",
                "Greetings, Sage. Shall I prepare for a question or a catastrophe?",
                "Sage. I was enjoying several seconds of administrative peace.",
                "There you are. I suppose my quiet afternoon was never going to last.",
                "Hello, Sage. I remain at your service, despite several strongly worded objections.",
                "Sage. How reassuring. The source of most of my paperwork has arrived."
            };
        }

        if (responseClass == "sage_question")
        {
            return new string[]
            {
                "I hear you, Sage. If the answer depends on something outside chat, you'll have to tell me that part.",
                "Ask plainly and I'll answer what I can from the information available here, Sage.",
                "I understand the question, Sage. Whether I have enough chat-side information to answer is the remaining concern.",
                "I'll help where I can, Sage. My knowledge ends where the chat stops.",
                "Question received. If this requires seeing the game or the stream, I will need you to describe it."
            };
        }

        if (responseClass == "social_observation")
        {
            if (pattern == "help_mockery_backhanded_defense")
            {
                return new string[]
                {
                    "A stirring defense. Unfortunately, you appear to have defended Sage by submitting additional evidence for the prosecution.",
                    "Sage asked for assistance, received mockery, and has now been defended with a second insult. Excellent work, everyone.",
                    "I appreciate the attempted defense of Sage. I question the decision to insult him more efficiently while doing it.",
                    "For the record, 'leave Sage alone because he's an idiot' is not generally considered a strong defense.",
                    "Chat has somehow converted a request for help into a collaborative performance review of Sage. Predictable."
                };
            }

            return new string[]
            {
                "That was technically a defense. The insult embedded inside it complicates the paperwork.",
                "An admirable attempt at support, undermined only by nearly everything that followed it.",
                "You defended Sage and insulted him in the same sentence. Efficient, if nothing else.",
                "I'm recording that as support with several concerning footnotes.",
                "I understand the intended defense. I also understand why Sage may prefer you had remained silent."
            };
        }

        if (responseClass == "sage_pile_on")
        {
            return new string[]
            {
                "I believe the point has been made. Repeatedly. With enthusiasm.",
                "Chat has moved beyond criticism and into peer-reviewed consensus.",
                "At this point you're no longer roasting Sage. You're conducting a panel discussion.",
                "The committee appears to have reached a verdict on Sage's performance.",
                "I would intervene, but Sage did provide most of the supporting evidence himself."
            };
        }

        if (responseClass == "weird_insult")
        {
            if (relationship == "trusted_menace" || relationship == "professional_nuisance")
            {
                return new string[]
                {
                    "Good to see you too, {user}. I see your vocabulary remains unsupervised.",
                    "There you are. And somehow the quality of discourse has immediately declined.",
                    "I've learned to interpret that as affection from you. I resent that development.",
                    "Another extraordinary contribution from my favorite administrative problem.",
                    "Your commitment to increasingly stupid insults is, regrettably, impressive."
                };
            }

            if (irritation >= 65 || grudge >= 65)
            {
                return new string[]
                {
                    "Another insult, {user}. Your commitment to this routine is exhausting.",
                    "I am familiar with your opinion. Repetition has not improved it.",
                    "An imaginative insult would at least justify the interruption.",
                    "You have my attention. Regrettably, not my admiration.",
                    "We have established that you disapprove. Consider the minutes saved by not repeating yourself."
                };
            }

            return new string[]
            {
                "That is certainly an insult. I am choosing to respect the confidence, if not the material.",
                "Fascinating. Thousands of years of language, and you selected that.",
                "I had not previously considered that an insult. I suppose I do now.",
                "Noted. I will file that under 'creative but professionally useless.'",
                "An inspired contribution to human language.",
                "You had every word available to you and still arrived there. Remarkable.",
                "I refuse to reward that with the level of confusion it deserves."
            };
        }

        if (responseClass == "repeated_joke")
        {
            return new string[]
            {
                "You've exhausted the comedic potential of that one.",
                "Yes, I remember. Repetition has not improved it.",
                "The joke has now survived longer than its premise.",
                "I acknowledged it once. That was already generous.",
                "Please allow the joke to die with what little dignity remains."
            };
        }

        if (responseClass == "hostile_boundary")
        {
            return new string[]
            {
                "You're welcome to dislike me. You're not required to turn it into everyone else's problem.",
                "Criticism is fine. The tantrum is unnecessary.",
                "I heard you. Repeating it more aggressively will not improve the argument.",
                "You appear to be confusing hostility with substance.",
                "If there was a useful point buried in that, the delivery has done an excellent job hiding it."
            };
        }

        if (responseClass == "reported_insult")
        {
            return new string[]
            {
                "Yes, I heard them. I survived the ordeal somehow.",
                "They did. I'm still deciding whether it qualifies as an insult or an anatomical observation.",
                "Correct. I have added it to an increasingly embarrassing incident log.",
                "I noticed. I was hoping everyone else had chosen not to.",
                "Unfortunately, yes. That happened."
            };
        }

        if (responseClass == "wellbeing")
        {
            return new string[]
            {
                "I'm functional. Given my working conditions, I'll take it.",
                "Operational, slightly tired, and still employed against my better judgment.",
                "I'm alright. I appreciate you asking.",
                "Managing. Sage has not yet created a crisis large enough to change that.",
                "Reasonably well, all things considered. Thank you."
            };
        }

        if (responseClass == "acknowledge_support")
        {
            return new string[]
            {
                "Thank you. I do occasionally appreciate this chat more than I admit.",
                "That's unexpectedly kind. I'll try not to make a habit of acknowledging it.",
                "I appreciate that, {user}.",
                "Noted. A genuinely pleasant interaction. How suspicious.",
                "Thank you. Please don't ruin the moment immediately."
            };
        }

        if (responseClass == "greeting")
        {
            return new string[]
            {
                "Hello, {user}.",
                "Good to see you, {user}.",
                "Welcome back, {user}.",
                "Hello. Things remain operational, somehow.",
                "Greetings. Please behave with at least marginal competence."
            };
        }

        if (responseClass == "sage_help")
        {
            return new string[]
            {
                "Sage has requested assistance. Please form an orderly line and lower your expectations.",
                "Chat, the boss is asking for help. Try to make your advice more useful than his current plan.",
                "A formal request for assistance has been issued. This is not a drill, although it may become one.",
                "Sage requires aid. I am documenting this entirely for administrative purposes.",
                "Chat, please assist Sage before confidence returns and makes the situation worse."
            };
        }

        if (responseClass == "sage_tease")
        {
            return new string[]
            {
                "I see chat has begun Sage's performance review without me.",
                "That assessment is unkind. I have not yet determined whether it is inaccurate.",
                "Please maintain professional standards while insulting my employer.",
                "I would defend Sage, but I'd prefer to review the evidence first.",
                "Noted. Sage's dignity continues to be treated as a community resource."
            };
        }

        if (responseClass == "complaint_about_apprentice_teasing")
        {
            return new string[]
            {
                "Soon? I prefer to establish expectations early.",
                "You addressed me directly. I assumed you were prepared for the consequences.",
                "I prefer to get the mandatory workplace hostility out of the way efficiently.",
                "You call it an insult. I call it an aggressively honest welcome.",
                "I was aiming for professional warmth. Apparently my calibration remains excellent.",
                "If that offended you, Sage, I should warn you that I have barely started complaining about my employment.",
                "You greeted me. I responded in accordance with established workplace culture.",
                "It wasn't personal. The sarcasm is simply part of the service package."
            };
        }

        if (responseClass == "addressed_question")
        {
            return new string[]
            {
                "I understand the question, but I do not have a supported answer for it from chat alone.",
                "I can read what you asked, but answering it reliably would require information I do not currently have.",
                "That is a real question. I simply do not have enough chat-side information to answer it responsibly.",
                "I would rather admit the limit than manufacture an answer.",
                "I understand what you're asking broadly. I do not have a reliable answer available from the information I can access."
            };
        }

        return new string[0];
    }

    private void AdjustSession(string name, int delta)
    {
        string marker = CPH.GetGlobalVar<string>(name + "_S", false);
        if (marker == null) return;

        int value = CPH.GetGlobalVar<int>(name, false);
        value += delta;

        if (value < 0) value = 0;
        if (value > 10000) value = 10000;

        CPH.SetGlobalVar(name, value, false);
    }
}
