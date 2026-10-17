using System;

public class CPHInline
{
    // Sages_Apprentice Observe Chat Pipeline v1.3
    //
    // Important order:
    // Language Analyzer -> classification repair -> Context Engine -> Reply Bridge
    // -> learning/memory/session -> Character Layer -> Attention Engine -> Response Brain
    //
    // The classification repair exists because Language Analyzer v2.1 can treat
    // addressed declarations such as "Sage apprentice is a dirty clanker" as
    // questions after stripping the address and seeing the word "is".

    private const string DefaultBotLogin = "sages_apprentice";

    public bool Execute()
    {
        string raw = "";
        string user = "";

        CPH.TryGetArg("rawInput", out raw);
        if (string.IsNullOrWhiteSpace(raw))
            CPH.TryGetArg("message", out raw);

        CPH.TryGetArg("user", out user);

        if (string.IsNullOrWhiteSpace(raw))
            return true;

        string botLogin = CPH.GetGlobalVar<string>("SA_CONFIG_BOT_LOGIN", true);
        if (string.IsNullOrWhiteSpace(botLogin))
            botLogin = DefaultBotLogin;

        if (!string.IsNullOrWhiteSpace(user) &&
            string.Equals(user, botLogin, StringComparison.OrdinalIgnoreCase))
            return true;

        string trimmed = raw.TrimStart();
        if (trimmed.StartsWith("!"))
            return true;

        CPH.SetArgument("saAttentionMode", "ambient");
        CPH.SetArgument("saLearningEnabled", true);
        CPH.SetArgument("saSource", "ambient_chat");

        if (!CPH.RunAction("SA - Language Analyzer", true))
            return true;

        // Repair known false-positive question classifications BEFORE the
        // Context Engine consumes them.
        RepairClassification(raw);

        if (!CPH.RunAction("SA - Context Engine", true))
            return true;

        if (!CPH.RunAction("SA - Phase2 Reply Bridge", true))
            return true;

        CPH.RunAction("SA - Persistent Learning", true);
        CPH.RunAction("SA - Behavior Memory", true);
        CPH.RunAction("SA - Session State", true);

        if (!CPH.RunAction("SA - Character Layer", true))
            return true;

        if (!CPH.RunAction("SA - Attention Engine", true))
            return true;

        bool shouldReact = false;
        CPH.TryGetArg("saShouldReact", out shouldReact);

        if (shouldReact)
            CPH.RunAction("SA - Response Brain", true);

        return true;
    }

    private void RepairClassification(string raw)
    {
        string norm = Read("saNormalized");
        if (string.IsNullOrWhiteSpace(norm))
            norm = Normalize(raw);

        string originalIntent = Read("saIntent");
        string originalSecondaryIntent = Read("saSecondaryIntent");
        string originalIntents = Read("saIntents");
        string target = Read("saTarget").ToLowerInvariant();
        bool originalQuestion = ReadBool("saIsQuestion");

        CPH.SetArgument("saOriginalIsQuestion", originalQuestion);
        CPH.SetArgument("saOriginalIntent", originalIntent ?? "");
        CPH.SetArgument("saOriginalSecondaryIntent", originalSecondaryIntent ?? "");
        CPH.SetArgument("saOriginalIntents", originalIntents ?? "");

        bool changed = false;
        string reason = "none";

        // A genuine wellbeing question should remain a question, but it should
        // not be mislabeled as a generic explanation/confusion request.
        if (target == "apprentice" && IsApprenticeWellbeingQuestion(norm))
        {
            CPH.SetArgument("saIsQuestion", true);
            CPH.SetArgument("saIntent", "wellbeing_check");
            CPH.SetArgument("saSecondaryIntent", "question");
            CPH.SetArgument("saIntents", "wellbeing_check,question,meta_bot");
            changed = !string.Equals(originalIntent, "wellbeing_check", StringComparison.OrdinalIgnoreCase);
            reason = changed ? "wellbeing_semantic_repair" : "none";
        }
        else
        {
            bool obviousInsult = target == "apprentice" && IsObviousApprenticeInsult(norm, originalIntent);
            bool addressedCopulaStatement = IsAddressedCopulaStatement(norm);
            bool strictQuestion = LooksLikeQuestionStrict(raw, norm);

            // If the analyzer called it a question but strict syntax says it is
            // declarative, correct it before context/behavior are calculated.
            if (originalQuestion && !strictQuestion && (obviousInsult || addressedCopulaStatement))
            {
                CPH.SetArgument("saIsQuestion", false);

                if (obviousInsult)
                {
                    CPH.SetArgument("saIntent", "insult");
                    CPH.SetArgument("saSecondaryIntent", "meta_bot");
                    CPH.SetArgument("saIntents", "insult,meta_bot");
                    reason = "declarative_insult_not_question";
                }
                else
                {
                    CPH.SetArgument("saIntent", "statement");
                    CPH.SetArgument("saSecondaryIntent", target == "apprentice" ? "meta_bot" : "");
                    CPH.SetArgument("saIntents", target == "apprentice" ? "statement,meta_bot" : "statement");
                    reason = "declarative_statement_not_question";
                }

                changed = true;
            }
            else if (obviousInsult &&
                     !string.Equals(originalIntent, "insult", StringComparison.OrdinalIgnoreCase))
            {
                // Even if there was no false question bit, repair the semantic
                // intent so later stages cannot collapse the insult into Q&A.
                CPH.SetArgument("saIntent", "insult");
                CPH.SetArgument("saSecondaryIntent", "meta_bot");
                CPH.SetArgument("saIntents", "insult,meta_bot");
                changed = true;
                reason = "apprentice_insult_semantic_repair";
            }
        }

        CPH.SetArgument("saClassificationRepairApplied", changed);
        CPH.SetArgument("saClassificationRepairReason", reason);
    }

    private bool LooksLikeQuestionStrict(string raw, string norm)
    {
        if (!string.IsNullOrWhiteSpace(raw) && raw.IndexOf('?') >= 0)
            return true;

        string text = StripApprenticeAddress(norm);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (StartsWithAny(text, new string[]
        {
            "who", "what", "when", "where", "why", "how", "which", "whose", "whom"
        }))
            return true;

        if (StartsWithAny(text, new string[]
        {
            "can", "could", "would", "will", "do", "does", "did", "should", "may", "might", "have", "has", "had"
        }))
            return true;

        // Copula questions need an actual subject after the auxiliary when an
        // Apprentice address has been removed. This distinguishes:
        // "Apprentice, are you okay" from "Apprentice is useless".
        if (StartsWithAny(text, new string[] { "is", "are", "was", "were" }))
        {
            string[] parts = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return false;

            string subject = parts[1];
            return IsLikelyQuestionSubject(subject, parts.Length);
        }

        return false;
    }

    private bool IsLikelyQuestionSubject(string word, int tokenCount)
    {
        if (word == "you" || word == "he" || word == "she" || word == "it" ||
            word == "we" || word == "they" || word == "i" || word == "this" ||
            word == "that" || word == "there" || word == "sage" || word == "sagezemage" ||
            word == "chat" || word == "anyone" || word == "anybody" || word == "everyone" ||
            word == "everybody" || word == "my" || word == "your" || word == "his" ||
            word == "her" || word == "our" || word == "their")
            return true;

        // "is the door open" is a likely question. Require enough words so
        // short declarations such as "is the worst" do not automatically pass.
        if (word == "the" && tokenCount >= 4)
            return true;

        return false;
    }

    private bool IsAddressedCopulaStatement(string norm)
    {
        if (string.IsNullOrWhiteSpace(norm))
            return false;

        string[] prefixes = new string[]
        {
            "apprentice is ", "apprentice are ", "apprentice was ", "apprentice were ",
            "sage apprentice is ", "sage apprentice are ", "sage apprentice was ", "sage apprentice were ",
            "sages apprentice is ", "sages apprentice are ", "sages apprentice was ", "sages apprentice were "
        };

        for (int i = 0; i < prefixes.Length; i++)
        {
            if (norm.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase))
            {
                string remainder = norm.Substring(prefixes[i].Length).Trim();
                if (string.IsNullOrWhiteSpace(remainder))
                    return false;

                string[] parts = remainder.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    return false;

                // If a clear subject follows the auxiliary, it may really be an
                // inverted question such as "Apprentice, is your code broken".
                if (IsLikelyQuestionSubject(parts[0], parts.Length + 1))
                    return false;

                return true;
            }
        }

        return false;
    }

    private bool IsApprenticeWellbeingQuestion(string norm)
    {
        if (Contains(norm, "how do you feel about"))
            return false;

        return Contains(norm, "how do you feel") ||
               Contains(norm, "how are you feeling") ||
               Contains(norm, "how are you") ||
               Contains(norm, "how you doing") ||
               Contains(norm, "are you okay") ||
               Contains(norm, "are you ok") ||
               Contains(norm, "you okay") ||
               Contains(norm, "you ok") ||
               Contains(norm, "you alright") ||
               Contains(norm, "you good");
    }

    private bool IsObviousApprenticeInsult(string norm, string intent)
    {
        if (string.Equals(intent, "insult", StringComparison.OrdinalIgnoreCase))
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
               Contains(norm, "idiot") ||
               Contains(norm, "moron") ||
               Contains(norm, "trash bot") ||
               Contains(norm, "garbage bot") ||
               Contains(norm, "bad bot") ||
               Contains(norm, "suck ass") ||
               Contains(norm, "sucks ass") ||
               Contains(norm, "piece of shit") ||
               Contains(norm, "shit bot") ||
               Contains(norm, "shitty bot");
    }

    private string StripApprenticeAddress(string norm)
    {
        if (string.IsNullOrWhiteSpace(norm))
            return "";

        string[] prefixes = new string[]
        {
            "hey apprentice ", "yo apprentice ", "ok apprentice ", "okay apprentice ", "well apprentice ",
            "sage apprentice ", "sages apprentice ", "apprentice "
        };

        for (int i = 0; i < prefixes.Length; i++)
        {
            if (norm.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase))
                return norm.Substring(prefixes[i].Length).Trim();
        }

        return norm.Trim();
    }

    private bool StartsWithAny(string text, string[] terms)
    {
        for (int i = 0; i < terms.Length; i++)
        {
            string term = terms[i];
            if (text == term || text.StartsWith(term + " ", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool Contains(string text, string phrase)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(phrase))
            return false;

        string a = " " + text.Trim() + " ";
        string b = " " + phrase.Trim() + " ";
        return a.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0;
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

    private string Read(string name)
    {
        string value = "";
        CPH.TryGetArg(name, out value);
        return value ?? "";
    }

    private bool ReadBool(string name)
    {
        bool value = false;
        CPH.TryGetArg(name, out value);
        return value;
    }
}
