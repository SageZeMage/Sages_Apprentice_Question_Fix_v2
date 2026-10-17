using System;

public class CPHInline
{
    // Sages_Apprentice Attention Engine v1.4
    //
    // Main changes from v1.2:
    // - questionLike no longer trusts broad semantic intents by itself
    // - Character Layer semantic classes have priority over generic Q&A
    // - explicit Apprentice insults beat addressed_question
    // - malfunction reports get a supported response class
    // - classification-repair diagnostics are preserved
    // - optional saBypassAttentionCooldown for deterministic testing

    public bool Execute()
    {
        string mode = "ambient";
        string user = "";
        string norm = "";
        string intent = "";
        string target = "";
        string behavior = "";
        string pattern = "";
        string relationship = "";
        string characterClass = "";
        string characterMotivation = "";
        int weirdInsult = 0;
        int hostility = 0;
        int playfulness = 0;
        int support = 0;
        int urgency = 0;
        int contextInterest = 0;
        int contextConfidence = 0;
        int socialBattery = 85;
        int patience = 75;
        int familiarity = 0;
        int irritation = 0;
        int grudge = 0;
        int characterBoost = 0;
        bool question = false;
        bool ambiguous = false;
        bool needsClarification = false;
        bool isBroadcaster = false;
        bool deterministic = false;
        bool bypassCooldown = false;
        bool implicitApprenticeTarget = false;
        bool continuationCandidate = false;
        bool earlyRepairApplied = false;

        CPH.TryGetArg("saAttentionMode", out mode);
        CPH.TryGetArg("user", out user);
        CPH.TryGetArg("saNormalized", out norm);
        CPH.TryGetArg("saIntent", out intent);
        CPH.TryGetArg("saEffectiveTarget", out target);
        CPH.TryGetArg("saBehaviorEvent", out behavior);
        CPH.TryGetArg("saContextPattern", out pattern);
        CPH.TryGetArg("saRelationship", out relationship);
        CPH.TryGetArg("saWeirdInsult", out weirdInsult);
        CPH.TryGetArg("saHostility", out hostility);
        CPH.TryGetArg("saPlayfulness", out playfulness);
        CPH.TryGetArg("saSupport", out support);
        CPH.TryGetArg("saUrgency", out urgency);
        CPH.TryGetArg("saContextInterest", out contextInterest);
        CPH.TryGetArg("saContextConfidence", out contextConfidence);
        CPH.TryGetArg("saSocialBattery", out socialBattery);
        CPH.TryGetArg("saPatience", out patience);
        CPH.TryGetArg("saFamiliarity", out familiarity);
        CPH.TryGetArg("saIrritation", out irritation);
        CPH.TryGetArg("saGrudge", out grudge);
        CPH.TryGetArg("saIsQuestion", out question);
        CPH.TryGetArg("saAmbiguous", out ambiguous);
        CPH.TryGetArg("saNeedsClarification", out needsClarification);
        CPH.TryGetArg("saIsBroadcaster", out isBroadcaster);
        CPH.TryGetArg("saDeterministicAttention", out deterministic);
        CPH.TryGetArg("saBypassAttentionCooldown", out bypassCooldown);
        CPH.TryGetArg("saImplicitApprenticeTarget", out implicitApprenticeTarget);
        CPH.TryGetArg("saContinuationCandidate", out continuationCandidate);
        CPH.TryGetArg("saCharacterResponseClass", out characterClass);
        CPH.TryGetArg("saCharacterMotivation", out characterMotivation);
        CPH.TryGetArg("saCharacterAttentionBoost", out characterBoost);
        CPH.TryGetArg("saClassificationRepairApplied", out earlyRepairApplied);

        intent = (intent ?? "").ToLowerInvariant();
        target = (target ?? "").ToLowerInvariant();
        behavior = (behavior ?? "").ToLowerInvariant();
        pattern = (pattern ?? "").ToLowerInvariant();
        norm = (norm ?? "").ToLowerInvariant();

        bool explicitApprenticeInsult =
            target == "apprentice" &&
            IsExplicitApprenticeInsult(norm, intent, behavior, hostility, weirdInsult);

        bool malfunctionReport =
            target == "apprentice" && IsMalfunctionReport(norm);

        // IMPORTANT: no broad IsQuestionLikeIntent(intent) here.
        // The old version let bad semantic labels keep a false question alive.
        bool questionLike = question || behavior == "question";

        bool complaintAboutTeasing =
            target == "apprentice" &&
            (behavior == "complaint_about_apprentice_teasing" || IsComplaintAboutApprenticeTeasing(norm));

        CPH.SetArgument("saQuestionLike", questionLike);
        CPH.SetArgument("saComplaintAboutTeasing", complaintAboutTeasing);

        string responseClass = ChooseResponseClass(
            characterClass,
            intent,
            target,
            behavior,
            pattern,
            weirdInsult,
            hostility,
            support,
            questionLike,
            norm,
            explicitApprenticeInsult,
            malfunctionReport);

        if (string.Equals(mode, "direct", StringComparison.OrdinalIgnoreCase))
        {
            CPH.SetArgument("saAttentionScore", 100);
            CPH.SetArgument("saAttentionThreshold", 0);
            CPH.SetArgument("saShouldReact", responseClass != "none");
            CPH.SetArgument("saResponseClass", responseClass);
            CPH.SetArgument("saAttentionReason", "direct_existing_command");
            SetClassificationDiagnostics(earlyRepairApplied, responseClass, questionLike, explicitApprenticeInsult, malfunctionReport, characterClass);
            return true;
        }

        int score = 6;
        int threshold = 70;
        string reasons = "base+6";

        bool namedApprentice =
            Contains(norm, "apprentice") ||
            Contains(norm, "sages apprentice") ||
            Contains(norm, "sage apprentice");

        characterBoost = Clamp(characterBoost, 0, 62);
        if (characterBoost > 0)
        {
            score += characterBoost;
            reasons += ",character+" + characterBoost;
        }

        if (target == "apprentice") { score += 28; reasons += ",target_apprentice+28"; }
        if (namedApprentice) { score += 18; reasons += ",named_apprentice+18"; }
        if (implicitApprenticeTarget) { score += 4; reasons += ",implicit_apprentice_target+4"; }
        if (questionLike && target == "apprentice") { score += 16; reasons += ",question_to_apprentice+16"; }

        if (explicitApprenticeInsult) { score += 20; reasons += ",apprentice_insult+20"; }
        else if (weirdInsult >= 35 && target == "apprentice") { score += 17; reasons += ",weird_insult+17"; }

        if (behavior == "hostile_insult" && target == "apprentice") { score += 21; reasons += ",hostile_insult+21"; }
        if (malfunctionReport) { score += 22; reasons += ",malfunction_report+22"; }
        if (behavior == "support" && target == "apprentice") { score += 10; reasons += ",support+10"; }

        if (target == "sage") { score += 8; reasons += ",sage_target+8"; }
        if (isBroadcaster) { score += 8; reasons += ",broadcaster+8"; }

        int contextBoost = Clamp((int)Math.Round(contextInterest * 0.55), -22, 30);
        score += contextBoost;
        if (contextBoost != 0) reasons += ",context" + Signed(contextBoost);

        if (pattern == "help_mockery_backhanded_defense") { score += 20; reasons += ",sequence+20"; }
        else if (pattern == "backhanded_defense") { score += 12; reasons += ",backhanded+12"; }
        else if (pattern == "pile_on") { score += 14; reasons += ",pile_on+14"; }
        else if (pattern == "callback") { score += 8; reasons += ",callback+8"; }
        else if (pattern == "conversation_continuation") { score += 6; reasons += ",turn_context+6"; }
        else if (pattern == "repeated_joke") { score -= 24; reasons += ",repetition-24"; }

        if (urgency >= 70) { score += 8; reasons += ",urgency+8"; }
        if (playfulness >= 70 && contextConfidence >= 60) { score += 5; reasons += ",playful_context+5"; }
        if (grudge >= 65 && target == "apprentice") { score += 6; reasons += ",grudge+6"; }
        if (familiarity >= 60) { score += 4; reasons += ",familiar+4"; }

        if (ambiguous && string.IsNullOrWhiteSpace(characterClass) && !explicitApprenticeInsult)
        {
            score -= 10;
            reasons += ",ambiguous-10";
        }

        if (needsClarification && string.IsNullOrWhiteSpace(characterClass))
        {
            score -= 9;
            reasons += ",needs_clarification-9";
        }

        if (socialBattery < 25) threshold += 10;
        if (socialBattery < 12) threshold += 10;
        if (patience < 20 && pattern == "repeated_joke") threshold += 12;

        long now = DateTime.UtcNow.Ticks;
        long lastSpoke = ParseLong(CPH.GetGlobalVar<string>("SA_ATT_LAST_SPOKE_TICKS", false));
        double secondsSinceSpoke = lastSpoke > 0 && now >= lastSpoke
            ? TimeSpan.FromTicks(now - lastSpoke).TotalSeconds
            : 99999;

        string lastReactUser = CPH.GetGlobalVar<string>("SA_ATT_LAST_REACT_USER", false) ?? "";
        long lastReactTicks = ParseLong(CPH.GetGlobalVar<string>("SA_ATT_LAST_REACT_TICKS", false));
        double userReactAge = lastReactTicks > 0 && now >= lastReactTicks
            ? TimeSpan.FromTicks(now - lastReactTicks).TotalSeconds
            : 99999;

        bool sameRecentUser =
            !string.IsNullOrWhiteSpace(user) &&
            string.Equals(lastReactUser, user, StringComparison.OrdinalIgnoreCase) &&
            userReactAge < 120;

        bool conversationContinuation =
            sameRecentUser &&
            target == "apprentice" &&
            (questionLike || complaintAboutTeasing || continuationCandidate || !string.IsNullOrWhiteSpace(characterClass));

        CPH.SetArgument("saConversationContinuation", conversationContinuation);

        if (!bypassCooldown)
        {
            if (conversationContinuation)
            {
                score += 20;
                reasons += ",conversation_continuation+20";

                if (secondsSinceSpoke < 15) { score -= 6; reasons += ",continuation_recent_speech-6"; }
                else if (secondsSinceSpoke < 45) { score -= 3; reasons += ",continuation_recent_speech-3"; }
            }
            else
            {
                if (secondsSinceSpoke < 30) { score -= 34; reasons += ",recent_speech-34"; }
                else if (secondsSinceSpoke < 90) { score -= 20; reasons += ",recent_speech-20"; }
                else if (secondsSinceSpoke < 180) { score -= 10; reasons += ",recent_speech-10"; }
                else if (secondsSinceSpoke > 600) { score += 7; reasons += ",long_silence+7"; }

                if (!string.IsNullOrWhiteSpace(user) &&
                    string.Equals(lastReactUser, user, StringComparison.OrdinalIgnoreCase) &&
                    userReactAge < 75)
                {
                    score -= 18;
                    reasons += ",same_user_cooldown-18";
                }
            }
        }
        else
        {
            reasons += ",cooldown_bypassed";
        }

        if (!deterministic)
        {
            int seed = unchecked((int)(DateTime.UtcNow.Ticks & 0x00000000FFFFFFFF));
            if (!string.IsNullOrWhiteSpace(user)) seed ^= user.Length * 397;
            Random random = new Random(seed);
            int jitter = random.Next(-3, 4);
            score += jitter;
            if (jitter != 0) reasons += ",jitter" + Signed(jitter);
        }

        score = Clamp(score, 0, 100);
        threshold = Clamp(threshold, 55, 95);

        bool supportedClass = responseClass != "none";
        bool shouldReact = score >= threshold && supportedClass;
        if (!supportedClass) shouldReact = false;

        CPH.SetArgument("saAttentionScore", score);
        CPH.SetArgument("saAttentionThreshold", threshold);
        CPH.SetArgument("saShouldReact", shouldReact);
        CPH.SetArgument("saResponseClass", responseClass);
        CPH.SetArgument("saAttentionReason", reasons);
        CPH.SetArgument("saImplicitTargetUsedByAttention", implicitApprenticeTarget);

        SetClassificationDiagnostics(earlyRepairApplied, responseClass, questionLike, explicitApprenticeInsult, malfunctionReport, characterClass);
        return true;
    }

    private string ChooseResponseClass(
        string characterClass,
        string intent,
        string target,
        string behavior,
        string pattern,
        int weirdInsult,
        int hostility,
        int support,
        bool question,
        string norm,
        bool explicitApprenticeInsult,
        bool malfunctionReport)
    {
        // Specific Character Layer semantics beat generic Q&A.
        if (!string.IsNullOrWhiteSpace(characterClass))
            return characterClass;

        if (pattern == "help_mockery_backhanded_defense" || pattern == "backhanded_defense") return "social_observation";
        if (pattern == "pile_on" && target == "sage") return "sage_pile_on";
        if (pattern == "repeated_joke" && target == "apprentice") return "repeated_joke";
        if (behavior == "reported_insult" && target == "apprentice") return "reported_insult";

        if (malfunctionReport) return "apprentice_malfunction";

        if (target == "apprentice" && (behavior == "hostile_insult" || hostility >= 70))
            return "hostile_boundary";

        if (target == "apprentice" && explicitApprenticeInsult)
            return "weird_insult";

        if ((behavior == "playful_absurd_insult" || behavior == "absurd_insult" || weirdInsult >= 35) && target == "apprentice")
            return "weird_insult";

        if (IsWellbeingQuestion(norm) && target == "apprentice") return "character_wellbeing";
        if (behavior == "support" && target == "apprentice") return "acknowledge_support";
        if ((behavior == "complaint_about_apprentice_teasing" || IsComplaintAboutApprenticeTeasing(norm)) && target == "apprentice") return "complaint_about_apprentice_teasing";
        if ((intent == "greeting" || behavior == "greeting") && target == "apprentice") return "greeting";
        if (behavior == "broadcaster_help_request") return "sage_help";
        if (behavior == "teasing" && target == "sage") return "sage_tease";

        // Generic question is intentionally the FINAL fallback.
        if (question && target == "apprentice") return "addressed_question";

        return "none";
    }

    private void SetClassificationDiagnostics(
        bool earlyRepairApplied,
        string responseClass,
        bool questionLike,
        bool explicitApprenticeInsult,
        bool malfunctionReport,
        string characterClass)
    {
        bool fixApplied = earlyRepairApplied;
        string reason = Read("saClassificationRepairReason");
        if (string.IsNullOrWhiteSpace(reason)) reason = "none";

        if (!string.IsNullOrWhiteSpace(characterClass) && responseClass == characterClass)
        {
            fixApplied = true;
            reason = AppendReason(reason, "character_semantic_override");
        }

        if (explicitApprenticeInsult && responseClass != "addressed_question")
        {
            fixApplied = true;
            reason = AppendReason(reason, "insult_over_question");
        }

        if (malfunctionReport && responseClass == "apprentice_malfunction")
        {
            fixApplied = true;
            reason = AppendReason(reason, "malfunction_semantic_override");
        }

        if (!questionLike && responseClass == "addressed_question")
        {
            fixApplied = true;
            reason = AppendReason(reason, "blocked_false_question");
        }

        CPH.SetArgument("saClassificationFixApplied", fixApplied);
        CPH.SetArgument("saClassificationFixReason", reason);
    }

    private string AppendReason(string existing, string next)
    {
        if (string.IsNullOrWhiteSpace(existing) || existing == "none") return next;
        if (existing.IndexOf(next, StringComparison.OrdinalIgnoreCase) >= 0) return existing;
        return existing + "," + next;
    }

    private bool IsExplicitApprenticeInsult(string norm, string intent, string behavior, int hostility, int weirdInsult)
    {
        if (intent == "insult" ||
            behavior == "hostile_insult" ||
            behavior == "playful_absurd_insult" ||
            behavior == "absurd_insult" ||
            weirdInsult >= 35 ||
            hostility >= 70)
            return true;

        return Contains(norm, "dirty clanker") ||
               Contains(norm, "clanker") ||
               Contains(norm, "useless ai") ||
               Contains(norm, "useless bot") ||
               Contains(norm, "worthless ai") ||
               Contains(norm, "worthless bot") ||
               Contains(norm, "stupid ai") ||
               Contains(norm, "stupid bot") ||
               Contains(norm, "dumb ai") ||
               Contains(norm, "dumb bot") ||
               Contains(norm, "trash bot") ||
               Contains(norm, "garbage bot") ||
               Contains(norm, "bad bot") ||
               Contains(norm, "idiot") ||
               Contains(norm, "moron") ||
               Contains(norm, "suck ass") ||
               Contains(norm, "sucks ass") ||
               Contains(norm, "piece of shit") ||
               Contains(norm, "shit bot") ||
               Contains(norm, "shitty bot");
    }

    private bool IsMalfunctionReport(string norm)
    {
        return Contains(norm, "malfunctioning") ||
               Contains(norm, "apprentice is broken") ||
               Contains(norm, "apprentice is bugged") ||
               Contains(norm, "bot is broken") ||
               Contains(norm, "bot is bugged") ||
               Contains(norm, "your apprentice is broken") ||
               Contains(norm, "your apprentice is malfunctioning");
    }

    private bool IsComplaintAboutApprenticeTeasing(string norm)
    {
        return Contains(norm, "why did you insult me") ||
               Contains(norm, "why did you have to insult me") ||
               Contains(norm, "why are you insulting me") ||
               Contains(norm, "why did you roast me") ||
               Contains(norm, "why did you have to roast me") ||
               Contains(norm, "why are you roasting me") ||
               Contains(norm, "why are you mean to me") ||
               Contains(norm, "why were you mean to me") ||
               Contains(norm, "did you have to insult me") ||
               Contains(norm, "did you have to roast me") ||
               Contains(norm, "you insulted me already") ||
               Contains(norm, "you roasted me already") ||
               Contains(norm, "insult me so soon") ||
               Contains(norm, "roast me so soon") ||
               Contains(norm, "that was mean") ||
               Contains(norm, "that was rude");
    }

    private bool IsWellbeingQuestion(string norm)
    {
        if (Contains(norm, "how do you feel about")) return false;

        return Contains(norm, "how do you feel") ||
               Contains(norm, "how are you feeling") ||
               Contains(norm, "are you okay") ||
               Contains(norm, "are you ok") ||
               Contains(norm, "you okay") ||
               Contains(norm, "you ok") ||
               Contains(norm, "you alright") ||
               Contains(norm, "how are you") ||
               Contains(norm, "how you doing") ||
               Contains(norm, "you good");
    }

    private bool Contains(string text, string phrase)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(phrase)) return false;
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

    private string Signed(int value) { return value >= 0 ? "+" + value : value.ToString(); }

    private long ParseLong(string text)
    {
        long value = 0;
        if (!string.IsNullOrWhiteSpace(text)) long.TryParse(text, out value);
        return value;
    }

    private int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
