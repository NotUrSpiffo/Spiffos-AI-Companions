using BepInEx.Configuration;
using UnityEngine;

namespace OfflineCompanionsAI
{
    public enum AiProvider
    {
        Player2,
        Ollama,
        LMStudio,
        Custom
    }

    public sealed class ModSettings
    {
        public const string GameKey = "OfflineCompanionsAI";

        public readonly ConfigEntry<ModLanguage> Language;

        public readonly ConfigEntry<AiProvider> Provider;
        public readonly ConfigEntry<string> BaseUrl;
        public readonly ConfigEntry<string> Model;
        public readonly ConfigEntry<string> ApiKey;
        public readonly ConfigEntry<float> Temperature;
        public readonly ConfigEntry<int> MaxTokens;
        public readonly ConfigEntry<int> TimeoutSeconds;

        public readonly ConfigEntry<KeyCode> TalkKey;
        public readonly ConfigEntry<bool> TalkInGameChat;
        public readonly ConfigEntry<bool> VoiceEnabled;
        public readonly ConfigEntry<KeyCode> VoiceKey;
        public readonly ConfigEntry<int> VoiceMaxSeconds;
        public readonly ConfigEntry<string> VoiceBaseUrl;
        public readonly ConfigEntry<bool> MemoryEnabled;
        public readonly ConfigEntry<bool> SummarizeMemory;
        public readonly ConfigEntry<bool> ReactionsEnabled;
        public readonly ConfigEntry<float> ReactionCooldown;
        public readonly ConfigEntry<int> ReactionsPerHour;
        public readonly ConfigEntry<bool> ReactBoss;
        public readonly ConfigEntry<bool> ReactBiome;
        public readonly ConfigEntry<bool> ReactPlayerDeath;
        public readonly ConfigEntry<bool> ReactCompanionReturn;
        public readonly ConfigEntry<bool> ReactLowHealth;
        public readonly ConfigEntry<bool> ReactGoal;
        public readonly ConfigEntry<bool> ReactNeeds;
        public readonly ConfigEntry<bool> ReactRaid;
        public readonly ConfigEntry<bool> ShowOrdersInChat;
        public readonly ConfigEntry<bool> ShowHints;
        public readonly ConfigEntry<bool> AllowEmotes;
        public readonly ConfigEntry<bool> AllowActions;
        public readonly ConfigEntry<KeyCode> EditModifier;
        public readonly ConfigEntry<float> ReadingSpeed;
        public readonly ConfigEntry<float> MinReadingSeconds;
        public readonly ConfigEntry<float> MaxReadingSeconds;
        public readonly ConfigEntry<float> TalkRange;
        public readonly ConfigEntry<float> ShoutRange;
        public readonly ConfigEntry<float> Cooldown;
        public readonly ConfigEntry<bool> ShowInChat;
        public readonly ConfigEntry<bool> ShareSpeech;

        public readonly ConfigEntry<string> ReplyLanguage;
        public readonly ConfigEntry<string> Personality;
        public readonly ConfigEntry<RebornSpeechMode> RebornSpeech;
        public readonly ConfigEntry<int> MaxReplyChars;
        public readonly ConfigEntry<int> HistoryMessages;

        public readonly ConfigEntry<AutonomyMode> Autonomy;
        public readonly ConfigEntry<float> AutonomyInterval;
        public readonly ConfigEntry<int> AutonomyPerHour;
        public readonly ConfigEntry<bool> AvoidStrongEnemies;
        public readonly ConfigEntry<float> StrongEnemyRatio;

        public ModSettings(ConfigFile cfg)
        {
            Language = cfg.Bind("Personaje", "Language", ModLanguage.Auto, "Mod UI, command and prompt language: Auto (matches Valheim's language: English, Spanish_Latam, Spanish), English, or Spanish. Can also be changed in-game from the Character Editor or with /language.");
            ModLang.Current = Language.Value;
            Language.SettingChanged += (_, __) =>
            {
                ModLang.Current = Language.Value;
                Companions.ModLocalization.Refresh();
            };

            Autonomy = cfg.Bind("Autonomia", "Mode", AutonomyMode.Normal, "Normal: only does what you ask. Autonomo: also speaks, suggests and performs useful tasks on their own, and each initiative spends balance. Toggle in-game with /autonomy (or /autonomia).");
            AutonomyInterval = cfg.Bind("Autonomia", "IntervalSeconds", 75f, new ConfigDescription("In autonomous mode, minimum seconds since they last spoke before taking the initiative. If busy they can only talk, and wait three times as long.", new AcceptableValueRange<float>(20f, 3600f)));
            AutonomyPerHour = cfg.Bind("Autonomia", "MaxPerHour", 30, new ConfigDescription("In autonomous mode, maximum initiatives per hour, aside from automatic comments. An initiative with a task spends two or more requests.", new AcceptableValueRange<int>(1, 120)));

            AvoidStrongEnemies = cfg.Bind("Autonomia", "AvoidStrongEnemies", true, "In autonomous mode, avoids going after enemies that are too strong for your gear and warns you. Still fights if they attack you or them up close, or if ordered. Bosses do not count.");
            StrongEnemyRatio = cfg.Bind("Autonomia", "StrongEnemyRatio", ThreatAssessment.DefaultLimit, new ConfigDescription("Threshold above which an enemy is considered too strong. Lower means more cautious. The log shows each enemy's ratio in [Threat] lines.", new AcceptableValueRange<float>(0.5f, 10f)));

            Provider = cfg.Bind("IA", "Provider", AiProvider.Player2, "AI service: Player2, Ollama, LMStudio or Custom.");
            BaseUrl = cfg.Bind("IA", "BaseUrl", "", "Only for Custom: OpenAI-compatible URL, including /v1. Example: https://openrouter.ai/api/v1");
            Model = cfg.Bind("IA", "Model", "", "Model to use. Player2 picks it for you. Required in Ollama (example: llama3.2) and most Custom services.");
            ApiKey = cfg.Bind("IA", "ApiKey", "", "Service API key, if needed. Stored unencrypted in this file: do not share it.");
            Temperature = cfg.Bind("IA", "Temperature", 0.8f, new ConfigDescription("Higher means more varied replies.", new AcceptableValueRange<float>(0f, 2f)));
            MaxTokens = cfg.Bind("IA", "MaxTokens", 0, "Token limit per reply. 0 = no limit. A low value may leave replies empty.");
            TimeoutSeconds = cfg.Bind("IA", "TimeoutSeconds", 30, new ConfigDescription("Seconds to wait before giving up.", new AcceptableValueRange<int>(5, 180)));

            TalkKey = cfg.Bind("Conversacion", "TalkKey", KeyCode.Y, "Key to talk to the companion you are looking at or the closest one. T does not work: Valheim uses it for emotes.");
            TalkInGameChat = cfg.Bind("Conversacion", "TalkInGameChat", true, "What you type in the in-game chat is also said to the nearby companion, and each message may spend balance. With other players in the game, only if you name them.");
            ShowOrdersInChat = cfg.Bind("Conversacion", "ShowOrdersInChat", true, "Shows in chat every order that was actually applied or rejected, aside from what the companion says.");
            ShowHints = cfg.Bind("Conversacion", "ShowHints", true, "Shows a key reminder when entering a world.");
            AllowEmotes = cfg.Bind("Conversacion", "AllowEmotes", true, "Allows the companion to accompany their lines with emotes, such as blowing a kiss or laughing, when calm and not working.");
            AllowActions = cfg.Bind("Conversacion", "AllowActions", true, "Allows the companion to obey what you ask in conversation: jobs, following you, stances, attacking, going to a place, and using what you look at.");
            EditModifier = cfg.Bind("Conversacion", "EditModifier", KeyCode.LeftShift, "Hold this key and press TalkKey to edit the character. You can also type /character (or /personaje) when talking to them.");
            ReadingSpeed = cfg.Bind("Conversacion", "ReadingSpeed", 12f, new ConfigDescription("Characters per second estimated for reading. Lower means longer time on screen.", new AcceptableValueRange<float>(4f, 40f)));
            MinReadingSeconds = cfg.Bind("Conversacion", "MinReadingSeconds", 7f, new ConfigDescription("Minimum time a reply stays on screen.", new AcceptableValueRange<float>(2f, 30f)));
            MaxReadingSeconds = cfg.Bind("Conversacion", "MaxReadingSeconds", 30f, new ConfigDescription("Maximum time a reply stays on screen.", new AcceptableValueRange<float>(5f, 120f)));
            TalkRange = cfg.Bind("Conversacion", "TalkRange", 80f, new ConfigDescription("Maximum distance in meters to talk with a companion. Should reach as far as they wander to work: beyond that you neither talk to them nor hear comments.", new AcceptableValueRange<float>(2f, 100f)));
            ShoutRange = cfg.Bind("Conversacion", "ShoutRange", 120f, new ConfigDescription("Maximum distance in meters at which a companion hears you when shouting with /s in chat. Beyond that, the game usually has their zone unloaded.", new AcceptableValueRange<float>(20f, 300f)));
            Cooldown = cfg.Bind("Conversacion", "Cooldown", 2f, new ConfigDescription("Minimum seconds between messages, to avoid overspending.", new AcceptableValueRange<float>(0f, 60f)));
            ShowInChat = cfg.Bind("Conversacion", "ShowInChat", true, "Logs the conversation in the chat window and shows it briefly. Press Enter to re-read it.");
            ShareSpeech = cfg.Bind("Multijugador", "ShareSpeech", true, "Other players who have this mod see what your companions say, in their speech bubble and in the chat, only within the normal talking distance of Valheim's chat. What you type to them is not sent.");

            ReactNeeds = cfg.Bind("Reacciones", "Needs", true, "Warns when hungry with no food, gear is very worn, or backpack is full, at most once every 10 minutes per need.");
            VoiceEnabled = cfg.Bind("Voz", "Enabled", true, "Allows talking to the companion with your microphone. Only listens while you hold VoiceKey. Requires the Player2 app, where microphone and language are selected, and spends balance.");
            VoiceKey = cfg.Bind("Voz", "VoiceKey", KeyCode.H, "Hold to speak and release to send.");
            VoiceMaxSeconds = cfg.Bind("Voz", "MaxSeconds", 15, new ConfigDescription("Maximum duration of each voice message.", new AcceptableValueRange<int>(VoiceProtocol.MinTimeout, VoiceProtocol.MaxTimeout)));
            VoiceBaseUrl = cfg.Bind("Voz", "BaseUrl", "http://localhost:4315/v1", "Address of the local Player2 app.");
            MemoryEnabled = cfg.Bind("Memoria", "Enabled", true, "Saves conversations between sessions, unencrypted, in the mod's config folder.");
            SummarizeMemory = cfg.Bind("Memoria", "Summarize", true, "Summarizes older messages with the AI to remember them long-term. Each summary is a request and spends balance.");
            ReactionsEnabled = cfg.Bind("Reacciones", "Enabled", true, "The companion comments on their own about what happens. Each comment is an AI request and spends balance.");
            ReactionCooldown = cfg.Bind("Reacciones", "Cooldown", 180f, new ConfigDescription("Minimum seconds since they last spoke, except when defeating a boss.", new AcceptableValueRange<float>(30f, 3600f)));
            ReactionsPerHour = cfg.Bind("Reacciones", "MaxPerHour", 10, new ConfigDescription("Maximum comments per hour of play.", new AcceptableValueRange<int>(1, 60)));
            ReactBoss = cfg.Bind("Reacciones", "BossDefeated", true, "Comments when defeating a boss.");
            ReactBiome = cfg.Bind("Reacciones", "NewBiome", true, "Comments when entering a biome for the first time in the session.");
            ReactPlayerDeath = cfg.Bind("Reacciones", "PlayerDied", true, "Comments when you die and respawn, if nearby.");
            ReactCompanionReturn = cfg.Bind("Reacciones", "CompanionReturned", true, "Comments when returning after dying.");
            ReactLowHealth = cfg.Bind("Reacciones", "LowHealth", true, "Comments when low on health.");
            ReactRaid = cfg.Bind("Reacciones", "Raid", true, "Comments when an enemy raid starts and ends where you are.");
            ReactGoal = cfg.Bind("Reacciones", "GoalFinished", true, "Comments when finishing an order like \"get 10 stones\". The on-screen notice always appears.");

            ReplyLanguage = cfg.Bind("Personaje", "ReplyLanguage", "auto", "Language in which the companion replies (follows Language setting if left as 'auto', 'English', or 'español').");
            Personality = cfg.Bind("Personaje", "Personality", "", "Shared personality for companions without custom instructions. Empty: each one gets a distinct profile.");
            RebornSpeech = cfg.Bind("Personaje", "RebornSpeech", RebornSpeechMode.Avisos, "Fixed lines from Offline Companions. Avisos (Warnings): only events and problems (\"I have no axe\"), without small talk or \"as you command\" that would speak over the character. Todas (All): like the original mod. Ninguna (None): total silence.");
            MaxReplyChars = cfg.Bind("Personaje", "MaxReplyChars", 220, new ConfigDescription("Maximum length of each text chunk above their head. Longer replies are shown in parts, and in full in the chat.", new AcceptableValueRange<int>(40, 600)));
            HistoryMessages = cfg.Bind("Personaje", "HistoryMessages", 12, new ConfigDescription("Recent messages sent in full to the AI on each request.", new AcceptableValueRange<int>(0, 64)));
        }

        public string ResolvedBaseUrl
        {
            get
            {
                switch (Provider.Value)
                {
                    case AiProvider.Player2: return "http://localhost:4315/v1";
                    case AiProvider.Ollama: return "http://localhost:11434/v1";
                    case AiProvider.LMStudio: return "http://localhost:1234/v1";
                    default: return (BaseUrl.Value ?? "").Trim().TrimEnd('/');
                }
            }
        }

        public string ResolvedModel => Provider.Value == AiProvider.Player2 ? null : Model.Value;
    }
}
