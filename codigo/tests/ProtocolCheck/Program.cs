using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OfflineCompanionsAI;

// Uso: ProtocolCheck [--live]
// Sin --live no usa la red. Con --live envía conversaciones reales a la app local de Player2 y gasta saldo.
internal static class Program
{
    private static int _failed;

    private static int Main(string[] args)
    {
        ModLang.Current = ModLanguage.Spanish;
        CheckResponses();
        CheckText();
        CheckRequests();
        CheckActions();
        CheckProfiles();
        CheckPrompts();
        CheckReactions();
        CheckMemory();
        CheckPlanning();
        CheckVoice();
        CheckGoals();
        CheckGiving();
        CheckIntent();
        CheckCookAndChests();
        CheckVersionComplete();
        CheckGroupOne();
        CheckBubble();
        CheckRolePlayNotes();
        CheckEmotes();
        CheckFirstPlaytest();
        CheckAutonomy();
        CheckFreezeVersion();
        CheckSearching();
        CheckEnglishMode();

        Console.WriteLine(_failed == 0 ? "OK: lógica sin red (ES + EN)" : "FALLOS: " + _failed);
        int sheetArg = Array.IndexOf(args, "--live-sheet");
        if (_failed == 0 && sheetArg >= 0 && args.Length > sheetArg + 2) LiveSheet(args[sheetArg + 1], args[sheetArg + 2]);
        if (_failed == 0 && Array.IndexOf(args, "--sizes") >= 0) PrintRequestSizes();
        int compareArg = Array.IndexOf(args, "--live-compare");
        if (_failed == 0 && compareArg >= 0) LiveCompare(args.Length > compareArg + 1 && int.TryParse(args[compareArg + 1], out int repeats) ? repeats : 2, args.Length > compareArg + 2 && int.TryParse(args[compareArg + 2], out int limit) ? limit : 0);
        if (_failed == 0 && Array.IndexOf(args, "--live-initiative") >= 0) LiveInitiative();
        if (_failed == 0 && Array.IndexOf(args, "--live-task") >= 0) LiveFinishedTask();
        if (_failed == 0 && Array.IndexOf(args, "--live-gear") >= 0) LiveGear();
        if (_failed == 0 && Array.IndexOf(args, "--live-food") >= 0) LiveFood();
        if (_failed == 0 && Array.IndexOf(args, "--live-context") >= 0) LiveContext();
        if (_failed == 0 && Array.IndexOf(args, "--live-short") >= 0) LiveShort();
        if (_failed == 0 && Array.IndexOf(args, "--live") >= 0) LiveOrders();
        if (_failed == 0 && Array.IndexOf(args, "--live-memory") >= 0) Live();
        if (_failed == 0 && Array.IndexOf(args, "--live-lasting") >= 0) LiveLastingMemory();
        return _failed == 0 ? 0 : 1;
    }

    private static void CheckEnglishMode()
    {
        ModLang.Current = ModLanguage.English;
        Check("EN ModLang.IsEnglish", ModLang.IsEnglish.ToString(), "True");
        Check("EN EffectiveReplyLanguage default", ModLang.EffectiveReplyLanguage("español"), "English");
        Check("EN EffectiveReplyLanguage custom", ModLang.EffectiveReplyLanguage("French"), "French");
        Check("EN LlmProtocol.DescribeError 402", LlmProtocol.DescribeError(402, null, "insufficient_credits").Contains("balance").ToString(), "True");
        Check("EN VoiceProtocol.DescribeError 404", VoiceProtocol.DescribeError(404, null).Contains("Listening time").ToString(), "True");
        Check("EN PromptBuilder system", PromptBuilder.Build(null, "Brave warrior", "English", "Name: Bjorn", "Events:\n- Day 1: arrived", true, true).Contains("Who you are and how you act:").ToString(), "True");
        Check("EN EmoteCatalog wave", EmoteCatalog.Extract("Hello there [GESTO: wave]", out string wave), "Hello there");
        Check("EN EmoteCatalog wave trigger", wave, "wave");
        Check("EN OrderIntent follow me", OrderIntent.Detect("follow me", new[] { "Bjorn" })?.Name, "come_here");
        Check("EN OrderIntent come here", OrderIntent.Detect("come here", new[] { "Bjorn" })?.Name, "come_here");
        Check("EN OrderIntent stop", OrderIntent.Detect("stop", new[] { "Bjorn" })?.Name, "cancel_orders");
        Check("EN OrderIntent stay here", OrderIntent.Detect("stay here", new[] { "Bjorn" })?.Name, "set_follow");
        Check("EN OrderIntent everyone follow me", OrderIntent.Detect("everyone follow me", new[] { "Bjorn" })?.Arguments.Contains("all").ToString(), "True");
        Check("EN CompanionActions gather Wood", CompanionActions.Parse("gather", "{\"item\":\"Wood\",\"amount\":10}", out string _)?.Description, "gather 10 wood");
        Check("EN CompanionActions stance passive", CompanionActions.StanceName(1), "passive");
        Check("EN Landmarks Crypt", Landmarks.Label("Crypt2"), "a burial chamber");
        Check("EN Landmarks Copper", Landmarks.Ore("rock4_copper"), "a copper vein");
        Check("EN PlaceBook home", PlaceBook.IsHomeWord("home").ToString(), "True");
        var mem = new CompanionMemory();
        mem.AddEvent(3, 2, "Slew Eikthyr", true);
        Check("EN CompanionMemory LongTerm", mem.LongTerm().Contains("Day 3: Slew Eikthyr").ToString(), "True");
        mem.ApplySummary("Summary text.\nFACT: My base is in the Meadows", 0);
        Check("EN CompanionMemory FACT", mem.LongTerm().Contains("My base is in the Meadows").ToString(), "True");
        ActionRequest fuelEn = CompanionActions.Parse("maintain", "{\"task\":\"fuel\"}", out string _);
        Check("EN furnaces: fill is not refuel", (CompanionActions.FurnaceMixup(fuelEn, "Can you fill the smelters?") != null).ToString(), "True");
        Check("EN furnaces: points to smelt job", CompanionActions.FurnaceMixup(fuelEn, "load the kiln with wood").Contains("set_job with smelt").ToString(), "True");
        Check("EN furnaces: fires named, respected", (CompanionActions.FurnaceMixup(fuelEn, "refuel the campfire by the furnace") == null).ToString(), "True");
        Check("EN furnaces: torches named, respected", (CompanionActions.FurnaceMixup(fuelEn, "fill the sconces near the smelter") == null).ToString(), "True");
        Check("EN maintain fuel label", fuelEn?.Description.Contains("campfires and torches").ToString(), "True");

        ModLang.Current = ModLanguage.Auto;
        ModLang.UpdateGameLanguage("Spanish_Latam");
        Check("Auto Spanish_Latam IsSpanish", ModLang.IsSpanish.ToString(), "True");
        Check("Auto Spanish_Latam EffectiveReplyLanguage", ModLang.EffectiveReplyLanguage("auto"), "español");
        ModLang.UpdateGameLanguage("Spanish");
        Check("Auto Spanish IsSpanish", ModLang.IsSpanish.ToString(), "True");
        ModLang.UpdateGameLanguage("English");
        Check("Auto English IsEnglish", ModLang.IsEnglish.ToString(), "True");
        Check("Auto English EffectiveReplyLanguage", ModLang.EffectiveReplyLanguage("auto"), "English");

        Check("Synonym ES Latam/España: hongos/setas", ItemNames.Matches("Setas", "hongos").ToString(), "True");
        Check("Synonym ES Latam/España: pedernal/sílex", ItemNames.Matches("Sílex", "pedernal").ToString(), "True");
        Check("Synonym ES Latam/España: madera fina/noble", ItemNames.Matches("Madera noble", "madera fina").ToString(), "True");
        Check("Synonym ES Latam/España: madera dura/resistente", ItemNames.Matches("Madera resistente", "madera dura").ToString(), "True");
        Check("Synonym ES Latam/España: carne cocida/asada", ItemNames.Matches("Carne asada", "carne cocida").ToString(), "True");
        Check("Synonym ES/EN: flint/pedernal", ItemNames.Matches("Flint", "pedernal").ToString(), "True");

        ModLang.Current = ModLanguage.Spanish;
    }

    private static void CheckResponses()
    {
        Check("texto normal", LlmProtocol.ParseResponse("{\"choices\":[{\"message\":{\"content\":\"Hola\"}}]}")?.Text, "Hola");
        Check("sin content (visto en Player2)", (LlmProtocol.ParseResponse("{\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\"},\"finish_reason\":\"stop\"}]}") == null).ToString(), "True");
        Check("JSON roto", (LlmProtocol.ParseResponse("<html>error</html>") == null).ToString(), "True");
        Check("elimina <think>", LlmProtocol.ParseResponse("{\"choices\":[{\"message\":{\"content\":\"<think>razono\\nmucho</think> Hola\"}}]}")?.Text, "Hola");
        Check("<think> sin cerrar", (LlmProtocol.ParseResponse("{\"choices\":[{\"message\":{\"content\":\"<think>no termino\"}}]}") == null).ToString(), "True");

        // Marcado interno que Player2 devolvió como texto en una prueba real.
        Check("marcado filtrado sin texto", (LlmProtocol.ParseResponse("{\"choices\":[{\"message\":{\"content\":\"<｜｜DSML｜｜ calls> <｜｜DSML｜｜ invoke name=\\\"repair_items\\\"> </｜｜DSML｜｜ invoke>\"}}]}") == null).ToString(), "True");
        Check("marcado filtrado tras texto", LlmProtocol.ParseResponse("{\"choices\":[{\"message\":{\"content\":\"Voy, capitán. <｜｜DSML｜｜ calls>\"}}]}")?.Text, "Voy, capitán.");

        // Forma exacta devuelta por Player2 en la prueba real.
        LlmResponse tools = LlmProtocol.ParseResponse("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"id\":\"call_00\",\"type\":\"function\",\"function\":{\"name\":\"set_job\",\"arguments\":\"{\\\"job\\\": \\\"wood\\\"}\"}}]},\"finish_reason\":\"tool_calls\"}]}");
        Check("acción: id", tools?.ToolCalls[0].Id, "call_00");
        Check("acción: nombre", tools?.ToolCalls[0].Name, "set_job");
        Check("acción: sin texto", tools?.Text, null);
    }

    private static void CheckText()
    {
        Check("Shorten corto", LlmProtocol.Shorten("Hola, amigo.", 50), "Hola, amigo.");
        Check("Shorten por frase", LlmProtocol.Shorten("Primera frase larga aquí. Segunda frase que no cabe.", 30), "Primera frase larga aquí.");
        Check("Shorten por palabra", LlmProtocol.Shorten("palabra palabra palabra palabra", 18), "palabra palabra...");
        Check("Shorten espacios", LlmProtocol.Shorten("  a\n\n b  ", 50), "a b");

        Check("quita narración", LlmProtocol.StripNarration("*Se ríe.* Claro, capitán. *Aprieta el hacha.*"), "Claro, capitán.");
        Check("quita narración sin cerrar", LlmProtocol.StripNarration("Claro. *Aprieta el"), "Claro.");
        Check("solo narración", LlmProtocol.StripNarration("*Asiente.*"), "Asiente.");
        Check("sin narración", LlmProtocol.StripNarration("Hola."), "Hola.");
        Check("narración pedida: se conserva", LlmProtocol.StripNarration("*Se ríe.*  Claro.", true), "*Se ríe.* Claro.");
        Check("narración pedida: sin herramientas escritas", LlmProtocol.StripNarration("<tool_call>x</tool_call> *Asiente.*", true), "*Asiente.*");
        var darknessLike = new CharacterProfile { Instructions = "Respuestas cortas. Describe una acción breve entre asteriscos si hace falta. No hables por el jugador.", Rules = "SIEMPRE:\n-Utiliza \"**\" para expresar acciones.\n- Habla en español." };
        Check("ficha: pide asteriscos", PromptBuilder.WantsNarration(darknessLike).ToString(), "True");
        Check("ficha: solo en reglas", PromptBuilder.WantsNarration(new CharacterProfile { Rules = "Usa ** para las acciones." }).ToString(), "True");
        Check("ficha: sin mencionarlos", PromptBuilder.WantsNarration(new CharacterProfile { Rules = "Habla en verso." }).ToString(), "False");
        Check("ficha: los prohíbe", PromptBuilder.WantsNarration(new CharacterProfile { Rules = "Nunca uses asteriscos." }).ToString(), "False");
        Check("ficha: vacía", PromptBuilder.WantsNarration(null).ToString(), "False");
        Check("ficha: la historia no cuenta", PromptBuilder.WantsNarration(new CharacterProfile { Lore = "Grabó un asterisco en su escudo." }).ToString(), "False");
        Check("prompt: con asteriscos pedidos", PromptBuilder.Build(darknessLike, "P", "español", "S").Contains("como pide tu ficha").ToString(), "True");
        Check("prompt: con asteriscos pedidos no los prohíbe", PromptBuilder.Build(darknessLike, "P", "español", "S").Contains("sin narrar acciones").ToString(), "False");
        Check("prompt: normal los prohíbe", PromptBuilder.Build(new CharacterProfile(), "P", "español", "S").Contains("sin narrar acciones").ToString(), "True");

        Check("lectura mínima", LlmProtocol.ReadingSeconds("Hola", 12f, 7f, 30f).ToString(), "7");
        Check("lectura por longitud", LlmProtocol.ReadingSeconds(new string('a', 120), 12f, 7f, 30f).ToString(), "10");
        Check("lectura máxima", LlmProtocol.ReadingSeconds(new string('a', 900), 12f, 7f, 30f).ToString(), "30");

        Check("error de conexión", LlmProtocol.DescribeError(0, "refused", null).StartsWith("No se pudo conectar").ToString(), "True");
        Check("sin saldo", LlmProtocol.DescribeError(402, null, "{\"error\":\"insufficient_credits\"}").Contains("saldo").ToString(), "True");
        Check("límite", LlmProtocol.DescribeError(429, null, "").Contains("Demasiadas").ToString(), "True");
    }

    private static void CheckRequests()
    {
        var messages = new List<ChatMessage> { new ChatMessage("user", "¿Qué tal, compañero?") };
        JObject withModel = JObject.Parse(LlmProtocol.BuildRequest(messages, "llama3.2", 100, 0.5f, CompanionActions.ToolDefinitions()));
        JObject plain = JObject.Parse(LlmProtocol.BuildRequest(messages, null, 0, 0.5f));
        Check("incluye model", (string)withModel["model"], "llama3.2");
        Check("incluye max_tokens", (string)withModel["max_tokens"], "100");
        Check("incluye tools", ((JArray)withModel["tools"]).Count.ToString(), "39");
        Check("sin model", plain["model"]?.ToString(), null);
        Check("sin max_tokens", plain["max_tokens"]?.ToString(), null);
        Check("sin tools", plain["tools"]?.ToString(), null);
        Check("conserva acentos", (string)plain["messages"][0]["content"], "¿Qué tal, compañero?");

        var round = new List<ChatMessage>
        {
            new ChatMessage("assistant", null, JArray.Parse("[{\"id\":\"c1\"}]")),
            new ChatMessage("tool", "Hecho.", null, "c1")
        };
        JObject second = JObject.Parse(LlmProtocol.BuildRequest(round, null, 0, 0.5f));
        Check("reenvía tool_calls", (string)second["messages"][0]["tool_calls"][0]["id"], "c1");
        Check("reenvía tool_call_id", (string)second["messages"][1]["tool_call_id"], "c1");
    }

    private static void CheckActions()
    {
        string error;
        ActionRequest wood = CompanionActions.Parse("set_job", "{\"job\":\"wood\"}", out error);
        Check("talar = modo 1", wood?.Value.ToString(), "1");
        Check("reparar = modo 15", CompanionActions.Parse("set_job", "{\"job\":\"repair\"}", out error)?.Value.ToString(), "15");
        Check("cocinar = modo 17", CompanionActions.Parse("set_job", "{\"job\":\"cook\"}", out error)?.Value.ToString(), "17");
        Check("sin trabajo = modo 0", CompanionActions.Parse("set_job", "{\"job\":\"none\"}", out error)?.Value.ToString(), "0");
        Check("pasiva = 1", CompanionActions.Parse("set_stance", "{\"stance\":\"passive\"}", out error)?.Value.ToString(), "1");
        Check("seguir", CompanionActions.Parse("set_follow", "{\"follow\":true}", out error)?.Value.ToString(), "1");
        Check("quedarse", CompanionActions.Parse("set_follow", "{\"follow\":false}", out error)?.Value.ToString(), "0");

        Check("atacar lo que mira", CompanionActions.Parse("attack", "{\"target\":\"looked_at\"}", out error)?.Value.ToString(), CompanionActions.TargetLookedAt.ToString());
        Check("atacar el más cercano", CompanionActions.Parse("attack", "{\"target\":\"nearest_enemy\"}", out error)?.Value.ToString(), CompanionActions.TargetNearest.ToString());
        Check("ir allí", CompanionActions.Parse("go_to", "{\"place\":\"looked_at\"}", out error)?.Kind.ToString(), "GoTo");
        Check("ir al jugador", CompanionActions.Parse("go_to", "{\"place\":\"player\"}", out error)?.Value.ToString(), CompanionActions.PlacePlayer.ToString());
        Check("dormir", CompanionActions.Parse("rest", "{\"kind\":\"sleep\"}", out error)?.Value.ToString(), CompanionActions.RestSleep.ToString());
        Check("usar lo que mira", CompanionActions.Parse("use_looked_at", "{}", out error)?.Kind.ToString(), "UseLookedAt");
        Check("guardar sin argumentos", CompanionActions.Parse("store_items", "", out error)?.Kind.ToString(), "StoreItems");
        Check("recuperar equipo", CompanionActions.Parse("recover_gear", "{}", out error)?.Kind.ToString(), "RecoverGear");
        Check("reparar equipo", CompanionActions.Parse("repair_gear", "{}", out error)?.Kind.ToString(), "RepairGear");
        Check("venir", CompanionActions.Parse("come_here", "{}", out error)?.Kind.ToString(), "ComeHere");
        Check("cancelar", CompanionActions.Parse("cancel_orders", "{}", out error)?.Kind.ToString(), "CancelOrders");

        Check("ir y quedarse", CompanionActions.Parse("go_to", "{\"place\":\"looked_at\",\"stay\":true}", out error)?.Stay.ToString(), "True");
        Check("ir sin quedarse", CompanionActions.Parse("go_to", "{\"place\":\"looked_at\"}", out error)?.Stay.ToString(), "False");
        Check("stay de texto no cuenta", CompanionActions.Parse("go_to", "{\"place\":\"looked_at\",\"stay\":\"true\"}", out error)?.Stay.ToString(), "False");
        Check("atacar es tarea", CompanionActions.IsExclusiveTask(ActionKind.Attack).ToString(), "True");
        Check("guardar es tarea", CompanionActions.IsExclusiveTask(ActionKind.StoreItems).ToString(), "True");
        Check("postura no es tarea", CompanionActions.IsExclusiveTask(ActionKind.SetStance).ToString(), "False");
        Check("trabajo no es tarea", CompanionActions.IsExclusiveTask(ActionKind.SetJob).ToString(), "False");

        Check("rechaza atacar sin objetivo", (CompanionActions.Parse("attack", "{}", out error) == null).ToString(), "True");
        Check("rechaza objetivo inventado", (CompanionActions.Parse("attack", "{\"target\":\"player\"}", out error) == null).ToString(), "True");
        Check("rechaza objetivo no textual", (CompanionActions.Parse("go_to", "{\"place\":1}", out error) == null).ToString(), "True");
        Check("rechaza trabajo inventado", (CompanionActions.Parse("set_job", "{\"job\":\"despawn\"}", out error) == null).ToString(), "True");
        Check("rechaza modo numérico", (CompanionActions.Parse("set_job", "{\"job\":\"4\"}", out error) == null).ToString(), "True");
        Check("rechaza acción inventada", (CompanionActions.Parse("despawn", "{}", out error) == null).ToString(), "True");
        Check("rechaza follow de texto", (CompanionActions.Parse("set_follow", "{\"follow\":\"true\"}", out error) == null).ToString(), "True");
        Check("rechaza JSON roto", (CompanionActions.Parse("set_job", "{job", out error) == null).ToString(), "True");
        Check("rechaza sin argumentos", (CompanionActions.Parse("set_job", "", out error) == null).ToString(), "True");
    }

    private static void CheckProfiles()
    {
        string folder = Path.Combine(Path.GetTempPath(), "ocai_profiles_" + Guid.NewGuid().ToString("N"));
        var store = new CharacterStore(Path.Combine(folder, "personajes"));
        try
        {
            string problem;
            Check("ficha inexistente", store.Load("abc", out problem).Instructions, "");
            Check("sin problema", problem, null);

            store.Save("abc", new CharacterProfile { Name = "Ñandú", Instructions = "Pirata\n\"tuerto\"", Lore = "Nació en Áfrika", Rules = "Di ¡arr!" });
            store.Save("abc", new CharacterProfile { Name = "Ñandú", Instructions = "Pirata\n\"tuerto\"", Lore = "Nació en África", Rules = "Di ¡arr!" });
            CharacterProfile loaded = store.Load("abc", out problem);
            Check("guarda acentos y saltos", loaded.Instructions, "Pirata\n\"tuerto\"");
            Check("sobrescribe", loaded.Lore, "Nació en África");
            Check("no deja temporales", File.Exists(store.PathFor("abc") + ".tmp").ToString(), "False");

            Check("ID sin rutas", Path.GetFileName(store.PathFor("..\\..\\x/y")), "xy.json");

            File.WriteAllText(store.PathFor("rota"), "{ no es json");
            Check("ficha dañada queda vacía", store.Load("rota", out problem).Instructions, "");
            Check("ficha dañada avisa", (problem != null).ToString(), "True");
            Check("ficha dañada se copia", File.Exists(store.PathFor("rota") + ".danado").ToString(), "True");

            var a = new CharacterProfile { Name = "A", Rules = "r" };
            var b = a.Clone();
            b.Name = "B";
            Check("el nombre no cuenta como cambio", a.SameContentAs(b).ToString(), "True");
            b.Rules = "otra";
            Check("detecta cambios", a.SameContentAs(b).ToString(), "False");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    private static void CheckPrompts()
    {
        string empty = PromptBuilder.Build(new CharacterProfile(), "PERSONALIDAD COMUN", "español", "SITUACION");
        Check("usa personalidad común", empty.Contains("PERSONALIDAD COMUN").ToString(), "True");
        Check("sin secciones vacías", (empty.Contains("Tu historia") || empty.Contains("Reglas que")).ToString(), "False");
        Check("incluye situación", empty.EndsWith("SITUACION").ToString(), "True");

        string full = PromptBuilder.Build(new CharacterProfile { Instructions = "INST", Lore = "HIST", Rules = "REGL" }, "PERSONALIDAD COMUN", "gallego", "S");
        Check("instrucciones sustituyen", full.Contains("PERSONALIDAD COMUN").ToString(), "False");
        Check("incluye todo", (full.Contains("INST") && full.Contains("HIST") && full.Contains("REGL") && full.Contains("gallego")).ToString(), "True");
    }

    private static void CheckCookAndChests()
    {
        string error;
        ActionRequest cook = CompanionActions.Parse("cook_food", "{\"item\":\"Carne\",\"amount\":3}", out error);
        Check("cocinar: tipo", cook?.Kind.ToString(), "Cook");
        Check("cocinar: cantidad", cook?.Amount.ToString(), "3");
        Check("cocinar: cantidad por defecto", CompanionActions.Parse("cook_food", "{\"item\":\"Carne\"}", out error)?.Amount.ToString(), CompanionActions.DefaultCookAmount.ToString());
        Check("cocinar: en cadena", CompanionActions.IsExclusiveTask(ActionKind.Cook).ToString(), "True");
        Check("cocinar: sin comida", (CompanionActions.Parse("cook_food", "{}", out error) == null).ToString(), "True");
        Check("cocinar: cantidad cero", (CompanionActions.Parse("cook_food", "{\"item\":\"Carne\",\"amount\":0}", out error) == null).ToString(), "True");
        Check("cocinar: cantidad excesiva", (CompanionActions.Parse("cook_food", "{\"item\":\"Carne\",\"amount\":5000}", out error) == null).ToString(), "True");
        Check("organizar: tipo", CompanionActions.Parse("organize_chests", "{}", out error)?.Kind.ToString(), "OrganizeChests");
        Check("organizar: en cadena", CompanionActions.IsExclusiveTask(ActionKind.OrganizeChests).ToString(), "True");
        Check("consultar: tipo", CompanionActions.Parse("check_chests", "{}", out error)?.Kind.ToString(), "CheckChests");
        Check("consultar: inmediato", CompanionActions.IsExclusiveTask(ActionKind.CheckChests).ToString(), "False");

        Check("objetos: madera negra", CompanionActions.JobForItem("Blackwood").ToString(), CompanionActions.JobWood.ToString());
        Check("objetos: obsidiana", CompanionActions.JobForItem("Obsidian").ToString(), CompanionActions.JobOre.ToString());
        Check("objetos: zanahoria", CompanionActions.JobForItem("Carrot").ToString(), "5");
        Check("objetos: carne", CompanionActions.JobForItem("DeerMeat").ToString(), "7");
        Check("objetos: pez", CompanionActions.JobForItem("Fish1").ToString(), "9");

        Check("sílex: con el nombre del juego", CompanionActions.Parse("gather", "{\"item\":\"Flint\",\"amount\":15}", out error)?.Description, "conseguir 15 de sílex");
        Check("sílex: solo del suelo", CompanionActions.OnlyOnGround("Flint").ToString(), "True");
        Check("sílex: la piedra sí se pica", CompanionActions.OnlyOnGround("Stone").ToString(), "False");
        Check("sílex: con el trabajo de piedra", CompanionActions.JobForItem("Flint").ToString(), CompanionActions.JobStone.ToString());

        var trap = new WaterTrapWatch();
        Check("lugares: marcar donde está el jugador", CompanionActions.Parse("mark_place", "{\"name\":\"la base\"}", out error)?.Value.ToString(), CompanionActions.MarkAtPlayer.ToString());
        Check("lugares: marcar donde mira", CompanionActions.Parse("mark_place", "{\"name\":\"mina\",\"at\":\"looked_at\"}", out error)?.Value.ToString(), CompanionActions.MarkLookedAt.ToString());
        Check("lugares: otro punto no vale", (CompanionActions.Parse("mark_place", "{\"name\":\"mina\",\"at\":\"luna\"}", out error) == null).ToString(), "True");
        Check("lugares: nombre obligatorio", (CompanionActions.Parse("go_to_place", "{}", out error) == null).ToString(), "True");
        Check("lugares: nombre con formato no vale", (CompanionActions.Parse("mark_place", "{\"name\":\"<b>base</b>\"}", out error) == null).ToString(), "True");
        Check("lugares: ir", CompanionActions.Parse("go_to_place", "{\"name\":\"base\"}", out error)?.Kind.ToString(), "GoToPlace");
        Check("lugares: ir es una tarea en fila", CompanionActions.IsExclusiveTask(ActionKind.GoToPlace).ToString(), "True");
        Check("lugares: marcar no interrumpe", CompanionActions.IsExclusiveTask(ActionKind.MarkPlace).ToString(), "False");
        Check("lugares: marcar no depende de quién", CompanionActions.IsWorldWide(ActionKind.MarkPlace).ToString(), "True");
        Check("lugares: por iniciativa no se marca", (Initiative.Refusal("mark_place", CompanionActions.Parse("mark_place", "{\"name\":\"x\"}", out error)) != null).ToString(), "True");
        Check("lugares: por iniciativa no se va lejos", (Initiative.Refusal("go_to_place", CompanionActions.Parse("go_to_place", "{\"name\":\"x\"}", out error)) != null).ToString(), "True");

        var book = new PlaceBook();
        book.Set("Base", 1f, 2f, 3f);
        book.Set("Mina de cobre", 4f, 5f, 6f);
        book.Set("Mina de estaño", 7f, 8f, 9f);
        Check("lugares: sin mayúsculas ni artículo", book.Find("la base")?.Name, "Base");
        Check("lugares: sin acentos", book.Find("mina de estano")?.Name, "Mina de estaño");
        Check("lugares: parte del nombre", book.Find("cobre")?.Name, "Mina de cobre");
        Check("lugares: si dos encajan, no adivina", (book.Find("mina") == null).ToString(), "True");
        Check("lugares: sinónimo de la base", book.Find("casa")?.Name, "Base");
        Check("lugares: lo que no existe", (book.Find("cueva") == null).ToString(), "True");
        Check("lugares: vacío", (book.Find(" ") == null).ToString(), "True");
        book.Set("la base", 10f, 11f, 12f);
        Check("lugares: marcar otra vez la mueve", book.Places.Count.ToString(), "3");
        Check("lugares: a su sitio nuevo", book.Find("base").X.ToString(), "10");
        var fullBook = new PlaceBook();
        for (int i = 0; i < PlaceBook.MaxPlaces; i++) fullBook.Set("sitio " + i, 0f, 0f, 0f);
        Check("lugares: llena, no admite otro", fullBook.Set("otro", 0f, 0f, 0f).ToString(), "False");
        Check("lugares: llena, sí mueve uno", fullBook.Set("sitio 1", 1f, 0f, 0f).ToString(), "True");

        var trip = new TravelGoal(0f);
        Check("viaje: al principio espera su turno", trip.Update(500f, 5f, 100f, 1f).ToString(), "Wait");
        Check("viaje: sigue andando", trip.Update(490f, 15f, 100f, 3.5f).ToString(), "Walk");
        Check("viaje: justo después no repite", trip.Update(485f, 20f, 100f, 4f).ToString(), "Wait");
        Check("viaje: antes de descargarse se adelanta", trip.Update(400f, 100f, 100f, 20f).ToString(), "Jump");
        Check("viaje: al llegar", new TravelGoal(0f).Update(TravelGoal.ArriveDistance, 400f, 100f, 1f).ToString(), "Arrived");
        var stuck = new TravelGoal(0f);
        stuck.Update(500f, 60f, 100f, 1f);
        Check("viaje: aún no está atascado", stuck.Update(500f, 60f, 100f, TravelGoal.StallSeconds).ToString() == "Jump" ? "Jump" : "Sigue", "Sigue");
        Check("viaje: atascado, se adelanta", stuck.Update(500f, 60f, 100f, 1f + TravelGoal.StallSeconds).ToString(), "Jump");
        var escorted = new TravelGoal(0f);
        escorted.Update(500f, 5f, 100f, 1f);
        Check("viaje: con el jugador al lado, aguanta más", escorted.Update(500f, 5f, 100f, 1f + TravelGoal.StallSeconds).ToString() == "Jump" ? "Jump" : "Sigue", "Sigue");
        Check("viaje: con el jugador al lado, también se rinde", escorted.Update(500f, 5f, 100f, 1f + TravelGoal.StallNearPlayerSeconds).ToString(), "Jump");
        var moving = new TravelGoal(0f);
        moving.Update(500f, 5f, 100f, 1f);
        moving.Update(490f, 5f, 100f, 10f);
        Check("viaje: avanzando no cuenta como atasco", moving.Update(480f, 5f, 100f, 20f).ToString() == "Jump" ? "Jump" : "Sigue", "Sigue");
        Check("posturas: pasiva", CompanionActions.StanceName(CompanionActions.StancePassive), "pasiva");
        Check("posturas: igual que la orden", CompanionActions.Parse("set_stance", "{\"stance\":\"passive\"}", out error)?.Value.ToString(), CompanionActions.StancePassive.ToString());

        Check("agua: nadando con aguante no cuenta", trap.Update(true, 0.8f, 0.25f, 0f).ToString(), "False");
        Check("agua: nada de más, aún no está atrapado", trap.Update(true, 0.1f, 0.25f, 100f).ToString(), "False");
        Check("agua: al poco de cansarse, aún no", trap.Update(true, 0.1f, 0.25f, 100f + WaterTrapWatch.TrappedSeconds - 1f).ToString(), "False");
        Check("agua: recuperar un poco parado no lo libra", trap.Update(true, 0.3f, 0.25f, 100f + WaterTrapWatch.TrappedSeconds - 0.5f).ToString(), "False");
        Check("agua: atrapado pasado el plazo", trap.Update(true, 0.1f, 0.25f, 100f + WaterTrapWatch.TrappedSeconds).ToString(), "True");
        trap.Update(false, 0.1f, 0.25f, 200f);
        Check("agua: salir del agua lo reinicia", trap.Update(true, 0.1f, 0.25f, 201f).ToString(), "False");
        trap.Update(true, 0.6f, 0.25f, 202f);
        Check("agua: recuperar de verdad lo reinicia", trap.Update(true, 0.1f, 0.25f, 203f + WaterTrapWatch.TrappedSeconds - 1f).ToString(), "False");
        trap.Reset();
        Check("agua: tras sacarlo vuelve a contar desde cero", trap.Update(true, 0.1f, 0.25f, 300f).ToString(), "False");
        Check("agua: ajuste a 0, nunca se da por cansado", new WaterTrapWatch().Update(true, 0f, 0f, 0f).ToString(), "False");

        var bouncing = new WaterTrapWatch();
        for (int second = 0; second < WaterTrapWatch.TrappedSeconds; second += 5) bouncing.Update(second % 10 == 0, 0.1f, 0.25f, second);
        Check("agua: hacer pie un momento no le libra del rescate", bouncing.Update(true, 0.1f, 0.25f, WaterTrapWatch.TrappedSeconds).ToString(), "True");
        var ashore = new WaterTrapWatch();
        ashore.Update(true, 0.1f, 0.25f, 0f);
        ashore.Update(false, 0.1f, 0.25f, WaterTrapWatch.AshoreSeconds);
        Check("agua: un rato en tierra sí reinicia el rescate", ashore.Update(true, 0.1f, 0.25f, WaterTrapWatch.TrappedSeconds).ToString(), "False");

        var rest = new WaterTrapWatch();
        rest.Update(true, 0.8f, 0.25f, 0f);
        Check("agua: con aguante puede nadar", rest.StayDry(0f).ToString(), "False");
        rest.Update(true, 0.2f, 0.25f, 1f);
        Check("agua: cansado no vuelve a nadar", rest.StayDry(1f).ToString(), "True");
        rest.Update(false, 0.3f, 0.25f, 3f);
        Check("agua: en tierra sin recuperarse, sigue sin nadar", rest.StayDry(3f).ToString(), "True");
        rest.Update(false, 0.5f, 0.25f, 6f);
        Check("agua: recuperado ya puede nadar", rest.StayDry(6f).ToString(), "False");

        var retreats = new WaterTrapWatch();
        for (int i = 0; i < WaterTrapWatch.RetreatsToLongRest; i++)
        {
            retreats.Update(true, 0.2f, 0.25f, i * 20f);
            if (i < WaterTrapWatch.RetreatsToLongRest - 1) retreats.Update(false, 0.6f, 0.25f, i * 20f + 10f);
        }
        float lastRetreat = (WaterTrapWatch.RetreatsToLongRest - 1) * 20f;
        Check("agua: tras volver agotado varias veces, descansa largo", retreats.OnLongRest(lastRetreat).ToString(), "True");
        retreats.Update(false, 1f, 0.25f, lastRetreat + 10f);
        Check("agua: el descanso largo sigue aunque recupere", retreats.StayDry(lastRetreat + 10f).ToString(), "True");
        Check("agua: el descanso largo se acaba", retreats.StayDry(lastRetreat + WaterTrapWatch.LongRestSeconds).ToString(), "False");

        var spaced = new WaterTrapWatch();
        for (int i = 0; i < WaterTrapWatch.RetreatsToLongRest; i++)
        {
            float at = i * (WaterTrapWatch.RetreatWindowSeconds + 1f);
            spaced.Update(true, 0.2f, 0.25f, at);
            spaced.Update(false, 0.6f, 0.25f, at + 10f);
        }
        Check("agua: cansarse de vez en cuando no le hace descansar largo", spaced.OnLongRest((WaterTrapWatch.RetreatsToLongRest - 1) * (WaterTrapWatch.RetreatWindowSeconds + 1f) + 10f).ToString(), "False");

        Check("guardar: si el cercano está lleno, otro", StorePlanner.Next(new[] { Carry("Stone", 30) }, new[] { Chest(1, 2f, 0), Chest(2, 9f, 3) }, null)?.Chest.ToString(), "2");
        Check("guardar: todos llenos", (StorePlanner.Next(new[] { Carry("Stone", 30) }, new[] { Chest(1, 2f, 0, "Wood"), Chest(2, 9f, 0, "Resin") }, null) == null).ToString(), "True");
        Check("guardar: sin cofres", (StorePlanner.Next(new[] { Carry("Stone", 30) }, new ChestSpace[0], null) == null).ToString(), "True");
        StoreTrip toHolder = StorePlanner.Next(new[] { Carry("Stone", 30), Carry("Wood", 10) }, new[] { Chest(1, 2f, 5), Chest(2, 9f, 5, "Stone") }, null);
        Check("guardar: prefiere donde ya hay", toHolder?.Chest.ToString(), "2");
        Check("guardar: y lleva solo lo de ese cofre", string.Join(",", toHolder?.Items ?? new HashSet<string>()), "Stone");
        StoreTrip toNearest = StorePlanner.Next(new[] { Carry("Wood", 10) }, new[] { Chest(1, 9f, 5), Chest(2, 2f, 5), Chest(3, 4f, 0) }, null);
        Check("guardar: lo que no está en ninguno, al más cercano con sitio", toNearest?.Chest.ToString(), "2");
        Check("guardar: si donde hay está lleno, a otro", StorePlanner.Next(new[] { Carry("Stone", 30) }, new[] { Chest(1, 9f, 2), Chest(2, 2f, 0, "Stone") }, null)?.Chest.ToString(), "1");
        ChestSpace someRoom = Chest(1, 2f, 0, "Stone");
        someRoom.RoomInStacks["Stone"] = 10;
        Check("guardar: vale aunque quepa solo una parte", StorePlanner.Next(new[] { Carry("Stone", 30) }, new[] { someRoom }, null)?.Chest.ToString(), "1");
        Check("guardar: no vuelve a un cofre que no sirvió", StorePlanner.Next(new[] { Carry("Stone", 30) }, new[] { Chest(1, 2f, 3), Chest(2, 9f, 3) }, new[] { 1 })?.Chest.ToString(), "2");
        Check("guardar: no más de lo que cabe", StorePlanner.WhatFits(new[] { Carry("A", 1), Carry("B", 1), Carry("C", 1) }, Chest(1, 2f, 2), false).Count.ToString(), "2");
        Check("guardar: dos pilas en una con sitio", StorePlanner.WhatFits(new[] { Carry("A", 10), Carry("A", 10) }, Chest(1, 2f, 1), false).Count.ToString(), "1");
        Check("guardar: cuenta unidades", StorePlanner.Units(new[] { Carry("A", 10), Carry("B", 5) }).ToString(), "15");

        var storing = new StoreGoal(30, 100f);
        storing.TripStarted(7);
        Check("guardar: con progreso sigue", storing.TripEnded(10).ToString() + storing.CanContinue(101f), "TrueTrue");
        Check("guardar: cofres usados", storing.ChestsUsed.ToString(), "1");
        storing.TripStarted(8);
        Check("guardar: un viaje en vano descarta el cofre", storing.TripEnded(10).ToString() + storing.Refused.Contains(8), "FalseTrue");
        storing.TripStarted(9);
        storing.TripEnded(0);
        Check("guardar: sin nada más, termina", storing.CanContinue(102f).ToString(), "False");
        Check("guardar: se agota el tiempo", new StoreGoal(5, 0f).CanContinue(StoreGoal.TimeoutSeconds).ToString(), "False");
        Check("objetos: gather con carne", CompanionActions.Parse("gather", "{\"item\":\"DeerMeat\",\"amount\":2}", out error)?.Value.ToString(), "7");

        var slowCook = new GatherGoal("Food", "comida", 2, 0, 0f, 120f, 120f);
        Check("cocina: paciente", slowCook.Update(0, false, 100f).ToString(), "Working");
        Check("cocina: atascada", slowCook.Update(0, false, 121f).ToString(), "Stuck");
        var sort = new TreeGoal(0f, 20f, 600f);
        Check("orden: trabajando", sort.Update(true, 0, 5f).ToString(), "Working");
        Check("orden: margen", sort.Update(false, 0, 6f).ToString(), "Working");
        Check("orden: termina", sort.Update(false, 0, 26f).ToString(), "Reached");
        Check("orden: tiempo agotado", new TreeGoal(0f, 20f, 600f).Update(true, 0, 600f).ToString(), "TimedOut");
    }

    private static void CheckSearching()
    {
        string error;
        Func<float, float, bool> anywhere = (x, z) => true;

        // Reborn integrado frente a una copia suelta.
        Check("reborn: la copia integrada no es otra", RebornCopy.IsForeign(@"C:\Valheim\BepInEx\plugins\SpiffoAiCompanions.dll", @"C:\Valheim\BepInEx\plugins\SpiffoAiCompanions.dll").ToString(), "False");
        Check("reborn: da igual mayúsculas o minúsculas", RebornCopy.IsForeign(@"c:\valheim\bepinex\plugins\spiffoaicompanions.dll", @"C:\Valheim\BepInEx\plugins\SpiffoAiCompanions.dll").ToString(), "False");
        Check("reborn: un Companions.dll suelto es otra copia", RebornCopy.IsForeign(@"C:\Valheim\BepInEx\plugins\OfflineCompanionsReborn\Companions.dll", @"C:\Valheim\BepInEx\plugins\SpiffoAiCompanions.dll").ToString(), "True");
        Check("reborn: el DLL con el nombre anterior es otra copia", RebornCopy.IsForeign(@"C:\Valheim\BepInEx\plugins\OfflineCompanionsAI.dll", @"C:\Valheim\BepInEx\plugins\SpiffoAiCompanions.dll").ToString(), "True");
        Check("reborn: sin ruta no se desactiva", RebornCopy.IsForeign(null, @"C:\Valheim\BepInEx\plugins\SpiffoAiCompanions.dll").ToString(), "False");

        // La rueda de órdenes.
        Check("rueda: seguir se queda", WheelOptions.Keeps(WheelOptions.Follow).ToString(), "True");
        Check("rueda: fijar hogar se queda", WheelOptions.Keeps(WheelOptions.SetHome).ToString(), "True");
        Check("rueda: despedir se queda", WheelOptions.Keeps(WheelOptions.Despawn).ToString(), "True");
        foreach (int gone in new[] { 1, 2, 3, 5, 6, 7, 8, 9, 12, 13, 14, 15, 17, 40, 41 })
        {
            Check("rueda: se quita la opción " + gone, WheelOptions.Keeps(gone).ToString(), "False");
        }
        Check("rueda: la IA no puede despedir", CompanionActions.ToolDefinitions().ToString().ToLowerInvariant().Contains("despawn").ToString(), "False");

        // Por dónde buscar.
        var plan = new SearchPlan(30f);
        plan.MarkSeen(5f, 5f);
        Check("búsqueda: recuerda lo pisado", plan.Seen(29f, 1f).ToString(), "True");
        Check("búsqueda: la celda de al lado no", plan.Seen(31f, 1f).ToString(), "False");
        Check("búsqueda: coordenadas negativas aparte", plan.Seen(-5f, 5f).ToString(), "False");
        bool moved = plan.Next(0f, 0f, 0f, 0f, 35f, 100f, anywhere, new Random(1), out float nextX, out float nextZ);
        Check("búsqueda: encuentra adónde ir", moved.ToString(), "True");
        Check("búsqueda: da un paso entero", (Math.Abs(Math.Sqrt(nextX * nextX + nextZ * nextZ) - 35.0) < 0.01).ToString(), "True");
        Check("búsqueda: nunca repite lo pisado", plan.Seen(nextX, nextZ).ToString(), "False");

        for (int seed = 0; seed < 20; seed++)
        {
            var leashed = new SearchPlan(30f);
            float x = 0f, z = 0f;
            for (int i = 0; i < 40 && leashed.Next(x, z, 0f, 0f, 35f, 87f, anywhere, new Random(seed + i), out float toX, out float toZ); i++)
            {
                if (Math.Sqrt(toX * toX + toZ * toZ) > 87.01) Check("búsqueda: no sale de la zona cargada", "fuera", "dentro");
                leashed.MarkSeen(toX, toZ);
                x = toX;
                z = toZ;
            }
        }
        Check("búsqueda: lejos del jugador y sin sitio, nada", new SearchPlan(30f).Next(500f, 500f, 0f, 0f, 35f, 87f, anywhere, new Random(1), out float _, out float _).ToString(), "False");
        Check("búsqueda: lejos, solo vuelve hacia el jugador", new SearchPlan(30f).Next(100f, 0f, 0f, 0f, 35f, 87f, anywhere, new Random(1), out float backX, out float _) && backX < 100f ? "True" : "False", "True");
        Check("búsqueda: sin paso posible, nada", new SearchPlan(30f).Next(0f, 0f, 0f, 0f, 35f, 87f, (x, z) => false, new Random(1), out float _, out float _).ToString(), "False");
        Check("búsqueda: junto al borde prueba un paso corto", new SearchPlan(30f).Next(0f, 0f, 0f, 0f, 35f, 25f, anywhere, new Random(1), out float _, out float _).ToString(), "True");
        for (int seed = 0; seed < 20; seed++)
        {
            new SearchPlan(30f).Next(0f, 0f, 0f, 0f, 35f, 100f, anywhere, new Random(seed), out float _, out float eastward, (x, z) => z > 0f, 50f);
            if (eastward <= 0f) Check("búsqueda: prefiere lo que falta en el mapa", "hacia lo conocido", "hacia lo nuevo");
        }
        var heading = new SearchPlan(30f);
        heading.Next(0f, 0f, 0f, 0f, 35f, 1000f, anywhere, new Random(3), out float firstX, out float firstZ);
        heading.MarkSeen(firstX, firstZ);
        heading.Next(firstX, firstZ, 0f, 0f, 35f, 1000f, anywhere, new Random(4), out float secondX, out float secondZ);
        Check("búsqueda: sigue en la misma dirección", (firstX * (secondX - firstX) + firstZ * (secondZ - firstZ) > 0f).ToString(), "True");

        // Cuándo cambiar de sitio.
        var goal = new ExploreGoal(0f);
        Check("explorar: andando", goal.Update(30f, 1f).ToString(), "Walk");
        Check("explorar: avanza", goal.Update(20f, 8f).ToString(), "Walk");
        Check("explorar: atascado", goal.Update(20f, 8f + ExploreGoal.StallSeconds).ToString(), "Blocked");
        goal.NewWaypoint(20f);
        Check("explorar: otro sitio, de cero", goal.Update(30f, 21f).ToString(), "Walk");
        Check("explorar: ha llegado", goal.Update(ExploreGoal.ArriveDistance, 22f).ToString(), "PickNext");
        goal.Hold(28f);
        goal.Update(30f, 28f);
        goal.Hold(100f);
        Check("explorar: pelear no es atascarse", goal.Update(30f, 101f).ToString(), "Walk");
        Check("explorar: se acaba el tiempo", goal.Update(30f, ExploreGoal.TimeLimitSeconds).ToString(), "OutOfTime");
        Check("explorar: no se congela al alejarse", (ExploreGoal.ReachShare < ExploreGoal.LeaveShare && ExploreGoal.LeaveShare < 1f).ToString(), "True");

        // Lo que encuentra.
        Check("lugar: cripta", Landmarks.Label("Crypt3"), "una cripta");
        Check("lugar: cripta hundida no es una cripta cualquiera", Landmarks.Label("SunkenCrypt2"), "una cripta hundida");
        Check("lugar: altar", Landmarks.Label("Eikthyrnir"), "el altar de Eikthyr");
        Check("lugar: ciudadela no es una ruina cualquiera", Landmarks.Label("Mistlands_DvergrTownEntrance1"), "la entrada de una ciudadela");
        Check("lugar: desconocido se cuenta igual", Landmarks.Label("LugarNuevoDeUnaVersion"), "un lugar fuera de lo normal");
        Check("lugar: el inicio no es un hallazgo", (Landmarks.Label("StartTemple") == null).ToString(), "True");
        Check("lugar: los mojones no son un hallazgo", (Landmarks.Label("Waymarker01") == null).ToString(), "True");
        Check("lugar: sin nombre", (Landmarks.Label("") == null).ToString(), "True");
        Check("veta: cobre", Landmarks.Ore("MineRock_Copper"), "una veta de cobre");
        Check("veta: plata", Landmarks.Ore("rock3_silver"), "una veta de plata");
        Check("veta: estaño", Landmarks.Ore("MineRock_Tin"), "una veta de estaño");
        Check("veta: una roca cualquiera no", (Landmarks.Ore("rock4_forest") == null).ToString(), "True");
        Check("mapa: sin artículo y con mayúscula", Landmarks.PinName("una cueva de trol"), "Cueva de trol");
        Check("mapa: unas ruinas", Landmarks.PinName("unas ruinas"), "Ruinas");

        // La orden.
        ActionRequest explore = CompanionActions.Parse("explore", "{}", out error);
        Check("orden: explorar", explore?.Kind.ToString(), "Explore");
        Check("orden: va en fila con las demás tareas", CompanionActions.IsExclusiveTask(ActionKind.Explore).ToString(), "True");
        Check("orden: la IA la tiene", CompanionActions.ToolDefinitions().ToString().Contains("\"explore\"").ToString(), "True");
        Check("iniciativa: puede explorar por su cuenta", (Initiative.Refusal("explore", explore) == null).ToString(), "True");
        Check("iniciativa: se le ofrece la herramienta", Initiative.Tools().ToString().Contains("\"explore\"").ToString(), "True");

        float ExploreIdea(Inclination inclination, InitiativeFacts facts = null) => Initiative.Ideas(facts ?? new InitiativeFacts(), key => false, inclination).Find(idea => idea.Tool == "explore")?.Weight ?? 0f;
        Check("iniciativa: pesa poco", ExploreIdea(Inclination.Ninguna).ToString(CultureInfo.InvariantCulture), "0.5");
        Check("iniciativa: a quien le gusta explorar, más", (ExploreIdea(Inclination.Exploracion) > ExploreIdea(Inclination.Ninguna)).ToString(), "True");
        Check("iniciativa: de noche fuera, no", ExploreIdea(Inclination.Exploracion, new InitiativeFacts { Night = true }).ToString(CultureInfo.InvariantCulture), "0");
        Check("iniciativa: con peligro, no", ExploreIdea(Inclination.Exploracion, new InitiativeFacts { EnemiesClose = true }).ToString(CultureInfo.InvariantCulture), "0");
        Check("iniciativa: tras explorar, otra cosa", Initiative.Ideas(new InitiativeFacts(), key => key == "explore").Exists(idea => idea.Tool == "explore").ToString(), "False");
    }

    private static void CheckFreezeVersion()
    {
        string error;

        // Estado de viaje.
        Check("estado: caminando", TravelGoal.Describe("base", false, 120, false, false), "ir a base y quedarte allí (a 120 m, caminando)");
        Check("estado: rodea agua", TravelGoal.Describe("base", false, 80, true, false), "ir a base y quedarte allí (a 80 m, rodeando agua)");
        Check("estado: atascado manda sobre rodear", TravelGoal.Describe("mina", true, 80, true, true), "ir a mina y encargarte de ese sitio (a 80 m, sin avanzar)");
        var slow = new TravelGoal(0f);
        slow.Update(100f, 10f, 100f, 3f);
        Check("estado: acaba de avanzar", slow.LooksStalled(5f).ToString(), "False");
        Check("estado: lleva un rato sin avanzar", slow.LooksStalled(10f).ToString(), "True");
        var left = new CompanionMemory();
        Check("estado: sin sitio", (left.DescribeStay() == null).ToString(), "True");
        left.SetStay("base", 1f, 2f, 3f, false);
        Check("estado: esperando", left.DescribeStay(), "esperando en base");
        left.SetStay("mina", 1f, 2f, 3f, true);
        Check("estado: encargándose", left.DescribeStay(), "encargándose de mina");
        CompanionMemory reloaded = JsonConvert.DeserializeObject<CompanionMemory>(JsonConvert.SerializeObject(left));
        Check("estado: sobrevive a cerrar el juego", reloaded.DescribeStay(), "encargándose de mina");
        Check("estado: quitar sitio avisa de guardar", reloaded.ClearStay().ToString(), "True");
        Check("estado: quitarlo otra vez no", reloaded.ClearStay().ToString(), "False");
        Check("memoria antigua: sin sitio", (JsonConvert.DeserializeObject<CompanionMemory>("{\"Summary\":\"x\"}").DescribeStay() == null).ToString(), "True");

        // Inclinación.
        Check("inclinación: ficha antigua sin el campo", (JsonConvert.DeserializeObject<CharacterProfile>("{\"Instructions\":\"x\"}").Inclination ?? "null"), "");
        Check("inclinación: ficha a mano, ninguna", Inclinations.Of(new CharacterProfile { Instructions = "Soy Darkness." }, "", "a").ToString(), "Ninguna");
        Check("inclinación: ficha a mano, elegida", Inclinations.Of(new CharacterProfile { Instructions = "Soy Darkness.", Inclination = "oficio" }, "", "a").ToString(), "Oficio");
        PersonalityPreset explorer = Array.Find(PersonalityPresets.All, preset => preset.Name == "Exploración");
        Check("inclinación: perfil elegido en el editor", Inclinations.Of(new CharacterProfile { Instructions = explorer.Text }, "", "a").ToString(), "Exploracion");
        Check("inclinación: ninguna manda sobre el perfil", Inclinations.Of(new CharacterProfile { Instructions = explorer.Text, Inclination = Inclinations.NoneValue }, "", "a").ToString(), "Ninguna");
        Check("inclinación: perfil por defecto", Inclinations.Of(new CharacterProfile(), "", "a").ToString(), PersonalityPresets.For("a").Leaning.ToString());
        Check("inclinación: personalidad común, ninguna", Inclinations.Of(new CharacterProfile(), "Soy común.", "a").ToString(), "Ninguna");
        Check("inclinación: cuenta como cambio de ficha", new CharacterProfile { Inclination = "caza" }.SameContentAs(new CharacterProfile()).ToString(), "False");
        Check("inclinación: no borra la conversación", new CharacterProfile { Inclination = "caza" }.SameTextAs(new CharacterProfile()).ToString(), "True");
        Check("inclinación: se copia", new CharacterProfile { Inclination = "caza" }.Clone().Inclination, "caza");

        var work = new InitiativeFacts { FiresLow = true, Animals = true, Trees = true, Ripe = true, LooseItems = true };
        float WeightOf(Inclination inclination, string text) => Initiative.Ideas(work, key => false, inclination).Find(idea => idea.Text.Contains(text)).Weight;
        Check("inclinación: sin ella, pesos de siempre", WeightOf(Inclination.Ninguna, "reavivar").ToString(CultureInfo.InvariantCulture), "3");
        Check("inclinación: oficio mantiene más", WeightOf(Inclination.Oficio, "reavivar").ToString(CultureInfo.InvariantCulture), "6");
        Check("inclinación: oficio caza menos", (WeightOf(Inclination.Oficio, "cazar") < WeightOf(Inclination.Ninguna, "cazar")).ToString(), "True");
        Check("inclinación: exploración busca más", (WeightOf(Inclination.Exploracion, "suelto") > WeightOf(Inclination.Ninguna, "suelto")).ToString(), "True");
        Check("inclinación: caza caza más", (WeightOf(Inclination.Caza, "cazar") > WeightOf(Inclination.Ninguna, "cazar")).ToString(), "True");
        Check("inclinación: recolección tala más", (WeightOf(Inclination.Recoleccion, "madera") > WeightOf(Inclination.Ninguna, "madera")).ToString(), "True");
        Check("inclinación: no quita ninguna idea", Initiative.Ideas(work, key => false, Inclination.Oficio).Count.ToString(), Initiative.Ideas(work, key => false).Count.ToString());
        Check("inclinación: con peligro tampoco propone", Initiative.Ideas(new InitiativeFacts { FiresLow = true, EnemiesClose = true }, key => false, Inclination.Oficio).Count.ToString(), "0");
        var nightFire = new InitiativeFacts { FiresLow = true, Night = true, AtBase = true, Trees = true, Rocks = true };
        Check("inclinación: lo urgente sigue siendo lo primero", Initiative.Pick(Initiative.Ideas(nightFire, key => false, Inclination.Exploracion), 3, new Random(1))[0].Text.Contains("reavivar").ToString(), "True");
        foreach (KeyValuePair<string, string> choice in Inclinations.Choices)
        {
            if (choice.Key.Length > 0) Check("inclinación: se entiende " + choice.Key, Inclinations.Of(new CharacterProfile { Instructions = "x", Inclination = choice.Key }, "", "a").ToString() == "Ninguna" ? (choice.Key == Inclinations.NoneValue).ToString() : "True", "True");
        }

        // Encárgate.
        ActionRequest plain = CompanionActions.Parse("go_to_place", "{\"name\":\"base\"}", out error);
        Check("encargo: ir sin más no se encarga", plain?.Tend.ToString(), "False");
        Check("encargo: ir sin más, descripción", plain?.Description, "ir a base y quedarse allí");
        ActionRequest tendBase = CompanionActions.Parse("go_to_place", "{\"name\":\"base\",\"tend\":true}", out error);
        Check("encargo: ve y encárgate", tendBase?.Tend.ToString(), "True");
        Check("encargo: lleva el lugar", tendBase?.Item, "base");
        Check("encargo: aquí, sin viaje", CompanionActions.Parse("go_to_place", "{\"name\":\"aquí\",\"tend\":true}", out error)?.Item, "");
        Check("encargo: sin nombre, aquí", CompanionActions.Parse("go_to_place", "{\"tend\":true}", out error)?.Item, "");
        Check("encargo: aquí sin encargo sigue siendo un nombre", CompanionActions.Parse("go_to_place", "{\"name\":\"aquí\"}", out error)?.Item, "aquí");
        Check("encargo: sin nombre ni encargo, error", (CompanionActions.Parse("go_to_place", "{}", out error) == null).ToString(), "True");
        Check("encargo: tend no booleano, no se encarga", CompanionActions.Parse("go_to_place", "{\"name\":\"base\",\"tend\":\"sí\"}", out error)?.Tend.ToString(), "False");
        Check("encargo: la herramienta lo ofrece", CompanionActions.ToolDefinitions().ToString().Contains("\"tend\"").ToString(), "True");
        Check("encargo: el tope es 10", TendPlan.GatherAmount.ToString(), "10");
        ActionRequest tendOrder = CompanionActions.GatherOrder("Wood", TendPlan.GatherAmount);
        Check("encargo: encargo de madera", tendOrder?.Kind + " " + tendOrder?.Amount + " " + tendOrder?.ItemLabel, "Gather 10 madera");
        Check("encargo: objeto desconocido", (CompanionActions.GatherOrder("Nada", 10) == null).ToString(), "True");

        TendSituation Spot() => new TendSituation { Trees = true, Rocks = true, Plant = "Raspberry", WoodStored = 5, StoneStored = 2, FoodStored = 0 };
        Check("encargo: oficio mantiene su reserva de madera", new TendPlan().NextGather(Inclination.Oficio, Spot(), 0f), "Wood");
        Check("encargo: exploración recoge plantas", new TendPlan().NextGather(Inclination.Exploracion, Spot(), 0f), "Raspberry");
        Check("encargo: recolección, lo que más falta", new TendPlan().NextGather(Inclination.Recoleccion, Spot(), 0f), "Stone");
        Check("encargo: caza sin presas cerca solo mantiene", (new TendPlan().NextGather(Inclination.Caza, Spot(), 0f) == null).ToString(), "True");
        TendSituation Prey() { TendSituation s = Spot(); s.Prey = "DeerMeat"; return s; }
        Check("encargo: caza va a por la presa cercana", new TendPlan().NextGather(Inclination.Caza, Prey(), 0f), "DeerMeat");
        Check("encargo: los demás no cazan", new TendPlan().NextGather(Inclination.Oficio, Prey(), 0f), "Wood");
        TendSituation fullLarder = Prey();
        fullLarder.FoodStored = TendPlan.Reserve;
        Check("encargo: con la despensa llena no caza", (new TendPlan().NextGather(Inclination.Caza, fullLarder, 0f) == null).ToString(), "True");
        var rested = new TendPlan();
        rested.Finished("DeerMeat", 2, 0f);
        Check("encargo: tras cazar descansa", (rested.NextGather(Inclination.Caza, Prey(), 1f) == null).ToString(), "True");
        Check("encargo: el alcance es de 50 m", TendPlan.AreaMeters.ToString(CultureInfo.InvariantCulture), "50");
        Check("encargo: sin inclinación solo mantiene", (new TendPlan().NextGather(Inclination.Ninguna, Spot(), 0f) == null).ToString(), "True");
        TendSituation With(Action<TendSituation> change) { TendSituation s = Spot(); change(s); return s; }
        Check("encargo: de noche no sale", (new TendPlan().NextGather(Inclination.Oficio, With(s => s.Night = true), 0f) == null).ToString(), "True");
        Check("encargo: mochila llena, no sale", (new TendPlan().NextGather(Inclination.Oficio, With(s => s.PackFull = true), 0f) == null).ToString(), "True");
        Check("encargo: sin hacha no tala", (new TendPlan().NextGather(Inclination.Oficio, With(s => s.NoAxe = true), 0f) == null).ToString(), "True");
        Check("encargo: sin árboles cerca no tala", (new TendPlan().NextGather(Inclination.Oficio, With(s => s.Trees = false), 0f) == null).ToString(), "True");
        Check("encargo: sin cofres no tiene dónde dejarlo", (new TendPlan().NextGather(Inclination.Oficio, With(s => s.WoodStored = null), 0f) == null).ToString(), "True");
        Check("encargo: con la reserva llena no sale", (new TendPlan().NextGather(Inclination.Oficio, With(s => s.WoodStored = TendPlan.Reserve), 0f) == null).ToString(), "True");
        Check("encargo: falta poco para la reserva, sale", new TendPlan().NextGather(Inclination.Oficio, With(s => s.WoodStored = TendPlan.Reserve - 1), 0f), "Wood");

        var partial = new TendPlan();
        partial.Finished("Wood", 4, 100f);
        Check("encargo: encontrar parte también vale, pero descansa", (partial.NextGather(Inclination.Oficio, Spot(), 100f + TendPlan.RestSeconds - 1f) == null).ToString(), "True");
        Check("encargo: tras descansar, sigue", partial.NextGather(Inclination.Oficio, Spot(), 100f + TendPlan.RestSeconds), "Wood");
        var empty = new TendPlan();
        empty.Finished("Wood", 0, 100f);
        Check("encargo: sin encontrar nada tarda más en reintentar", (empty.NextGather(Inclination.Oficio, Spot(), 100f + TendPlan.RestSeconds) == null).ToString(), "True");
        Check("encargo: al final lo reintenta", empty.NextGather(Inclination.Oficio, Spot(), 100f + TendPlan.NothingFoundSeconds), "Wood");
        Check("encargo: fallar con madera no impide la piedra", empty.NextGather(Inclination.Recoleccion, Spot(), 100f), "Stone");

        // Recuerdos de expediciones.
        Check("expedición: solo", ExpeditionText.Arrival("La mina", null), "Llegué por primera vez a La mina.");
        Check("expedición: con uno", ExpeditionText.Arrival("La mina", new[] { "Ana" }), "Llegué por primera vez a La mina con Ana.");
        Check("expedición: con tres", ExpeditionText.Arrival("La mina", new[] { "Ana", "Bo", "Cid" }), "Llegué por primera vez a La mina con Ana, Bo y Cid.");
        Check("expedición: con más", ExpeditionText.Arrival("La mina", new[] { "Ana", "Bo", "Cid", "Dan" }), "Llegué por primera vez a La mina con Ana, Bo, Cid y otros.");
        Check("expedición: susto en un lugar", ExpeditionText.NearDeath("La mina"), "Estuve a punto de morir en La mina y salí con vida.");
        Check("expedición: susto sin lugar, como siempre", ExpeditionText.NearDeath(null), "Estuve a punto de morir y salí con vida.");
        Check("expedición: muerte sin lugar, como siempre", ExpeditionText.Death(null), "Morí y volví a la vida.");
        Check("expedición: incursión sin lugar, como siempre", ExpeditionText.Raid(null), "Aguantamos una incursión de enemigos.");
        Check("expedición: el jugador", ExpeditionText.PlayerNearDeath("Ana", "La mina"), "Ana estuvo a punto de morir en La mina y salió adelante a mi lado.");

        var traveler = new CompanionMemory();
        Check("expedición: primera vez", traveler.MarkVisited("La mina").ToString(), "True");
        Check("expedición: volver no cuenta", traveler.MarkVisited("la MINA").ToString(), "False");
        traveler.Forget();
        Check("expedición: olvidar todo también lo olvida", traveler.MarkVisited("La mina").ToString(), "True");

        var crowded = new CompanionMemory();
        crowded.AddEvent(1, MemoryWeight.Boss, "Derrotamos a Eikthyr.", true);
        crowded.AddEvent(2, MemoryWeight.Death, "Morí y volví a la vida.");
        crowded.AddEvent(3, MemoryWeight.Notable, "Estuve a punto de morir y salí con vida.");
        for (int i = 0; i < CompanionMemory.MaxEvents * 2; i++) crowded.AddEvent(4 + i, MemoryWeight.Visit, "Llegué por primera vez a lugar " + i + ".", true);
        Check("expedición: no llena la memoria", crowded.Events.Count.ToString(), CompanionMemory.MaxEvents.ToString());
        Check("expedición: no desplaza al jefe", crowded.Events.Exists(e => e.Text.Contains("Eikthyr")).ToString(), "True");
        Check("expedición: no desplaza la muerte", crowded.Events.Exists(e => e.Text.StartsWith("Morí")).ToString(), "True");
        Check("expedición: no desplaza el susto", crowded.Events.Exists(e => e.Text.StartsWith("Estuve")).ToString(), "True");
        Check("expedición: se queda con las más recientes", crowded.Events.Exists(e => e.Text.Contains("lugar " + (CompanionMemory.MaxEvents * 2 - 1))).ToString(), "True");
    }

    private static void CheckAutonomy()
    {
        string error;
        ActionRequest looked = CompanionActions.Parse("pick_up_item", "{}", out error);
        Check("recoger lo que mira", looked?.Kind.ToString(), "PickUpItem");
        Check("recoger: sin nombre", looked?.Item, "");
        Check("recoger: por nombre", CompanionActions.Parse("pick_up_item", "{\"item\":\" hacha \"}", out error)?.Item, "hacha");
        Check("recoger: nombre no textual", CompanionActions.Parse("pick_up_item", "{\"item\":3}", out error)?.Item, "");
        Check("recoger: espera su turno", CompanionActions.IsExclusiveTask(ActionKind.PickUpItem).ToString(), "True");

        var known = new HashSet<string>();
        foreach (JToken tool in CompanionActions.ToolDefinitions()) known.Add((string)tool["function"]["name"]);
        foreach (string name in Initiative.AllowedToolNames) Check("iniciativa: existe " + name, known.Contains(name).ToString(), "True");
        Check("iniciativa: se ofrecen todas", Initiative.Tools().Count.ToString(), new List<string>(Initiative.AllowedToolNames).Count.ToString());

        ActionRequest Parsed(string tool, string args) => CompanionActions.Parse(tool, args, out error);
        Check("iniciativa: no ataca", (Initiative.Refusal("attack", Parsed("attack", "{\"target\":\"nearest_enemy\"}")) != null).ToString(), "True");
        Check("iniciativa: no gasta materiales", (Initiative.Refusal("smelt_item", Parsed("smelt_item", "{\"item\":\"cobre\"}")) != null).ToString(), "True");
        Check("iniciativa: no cambia ajustes", (Initiative.Refusal("set_home", Parsed("set_home", "{}")) != null).ToString(), "True");
        Check("iniciativa: no entrega objetos", (Initiative.Refusal("give_items", Parsed("give_items", "{\"item\":\"madera\"}")) != null).ToString(), "True");
        Check("iniciativa: no va a donde se mira", (Initiative.Refusal("go_to", Parsed("go_to", "{\"place\":\"looked_at\"}")) != null).ToString(), "True");
        Check("iniciativa: no se queda en otro sitio", (Initiative.Refusal("go_to", Parsed("go_to", "{\"place\":\"chest\",\"stay\":true}")) != null).ToString(), "True");
        Check("iniciativa: va al cofre", Initiative.Refusal("go_to", Parsed("go_to", "{\"place\":\"chest\"}")), null);
        Check("iniciativa: encargo pequeño", Initiative.Refusal("gather", Parsed("gather", "{\"item\":\"Wood\",\"amount\":20}")), null);
        Check("iniciativa: encargo grande", (Initiative.Refusal("gather", Parsed("gather", "{\"item\":\"Wood\",\"amount\":21}")) != null).ToString(), "True");

        // Orden de atacar a un animal: no debe quedarse parado para siempre.
        var hunt = new HuntWatch(0f, 0f, 100f, 20, 0f);
        Check("caza: empieza", hunt.Update(true, 100f, true, 0f, 0f, 20, 1f).ToString(), "Going");
        Check("caza: quieto un rato", hunt.Update(true, 100f, true, 0f, 0f, 20, 29f).ToString(), "Going");
        Check("caza: moverse es avanzar", hunt.Update(true, 100f, true, 3f, 0f, 20, 29.5f).ToString(), "Going");
        Check("caza: sigue el plazo", hunt.Update(true, 100f, true, 3f, 0f, 20, 50f).ToString(), "Going");
        Check("caza: disparar es avanzar", hunt.Update(true, 100f, true, 3f, 0f, 19, 58f).ToString(), "Going");
        Check("caza: herirlo es avanzar", hunt.Update(true, 80f, true, 3f, 0f, 19, 85f).ToString(), "Going");
        Check("caza: un paso no cuenta", hunt.Update(true, 80f, true, 3.5f, 0f, 19, 100f).ToString(), "Going");
        Check("caza: atascado", hunt.Update(true, 80f, true, 3.5f, 0f, 19, 115f).ToString(), "Stalled");
        Check("caza: lo mata", new HuntWatch(0f, 0f, 100f, 20, 0f).Update(false, 0f, true, 0f, 0f, 20, 40f).ToString(), "Done");

        var fled = new HuntWatch(0f, 0f, 100f, 20, 0f);
        Check("caza: un momento sin pelear", fled.Update(true, 100f, false, 0f, 0f, 20, 4f).ToString(), "Going");
        Check("caza: deja de perseguirlo", fled.Update(true, 100f, false, 0f, 0f, 20, 5f).ToString(), "Lost");

        var chase = new HuntWatch(0f, 0f, 100f, 20, 0f);
        for (int t = 10; t < HuntWatch.TimeoutSeconds; t += 10) Check("caza: persiguiendo " + t, chase.Update(true, 100f, true, t, 0f, 20, t).ToString(), "Going");
        Check("caza: persecución sin fin", chase.Update(true, 100f, true, 999f, 0f, 20, HuntWatch.TimeoutSeconds).ToString(), "Stalled");

        // Más variedad, con límites.
        Check("iniciativa: ordena cofres", Initiative.Refusal("organize_chests", Parsed("organize_chests", "{}")), null);
        Check("iniciativa: se equipa", Initiative.Refusal("equip_item", Parsed("equip_item", "{\"item\":\"arco\"}")), null);
        Check("iniciativa: cocina poco", Initiative.Refusal("cook_food", Parsed("cook_food", "{\"item\":\"carne\"}")), null);
        Check("iniciativa: no cocina mucho", (Initiative.Refusal("cook_food", Parsed("cook_food", "{\"item\":\"carne\",\"amount\":6}")) != null).ToString(), "True");
        Check("iniciativa: va a un cofre encontrado", Initiative.Refusal("go_to", Parsed("go_to", "{\"place\":\"found_chest\"}")), null);
        Check("cofre encontrado: su lugar", Parsed("go_to", "{\"place\":\"found_chest\"}")?.Value.ToString(), CompanionActions.PlaceFoundChest.ToString());

        Check("variedad: por tipo de recurso", Initiative.VarietyKey("gather", Parsed("gather", "{\"item\":\"Stone\",\"amount\":5}")), "gather:" + CompanionActions.JobStone);
        Check("variedad: cazar es una sola cosa", Initiative.VarietyKey("gather", Parsed("gather", "{\"item\":\"DeerMeat\",\"amount\":5}")), Initiative.VarietyKey("gather", Parsed("gather", "{\"item\":\"RawMeat\",\"amount\":5}")));
        Check("variedad: recoger es una sola cosa", Initiative.VarietyKey("pick_up_item", Parsed("pick_up_item", "{\"item\":\"arco\"}")), "pick_up_item");
        Check("variedad: comer puede repetirse", Initiative.VarietyKey("consume_item", Parsed("consume_item", "{\"item\":\"food\"}")), null);
        Check("variedad: cocinar es una sola cosa", Initiative.VarietyKey("cook_food", Parsed("cook_food", "{\"item\":\"carne\"}")), "cook_food");
        Check("variedad: mantenimiento por tarea", Initiative.VarietyKey("maintain", Parsed("maintain", "{\"task\":\"fuel\"}")), "maintain:PlanFuel");

        var history = new InitiativeHistory();
        history.Add("gather:Stone", "conseguir piedra", 100f);
        Check("historial: reciente", history.WasRecent("gather:Stone", 300f).ToString(), "True");
        Check("historial: otra cosa", history.WasRecent("gather:Wood", 300f).ToString(), "False");
        Check("historial: pasado el tiempo", history.WasRecent("gather:Stone", 100f + InitiativeHistory.RepeatSeconds).ToString(), "False");
        Check("historial: qué hizo", string.Join("|", history.Recent(300f)), "conseguir piedra");
        Check("historial: ya no cuenta", history.Recent(100f + InitiativeHistory.RepeatSeconds).Count.ToString(), "0");

        var facts = new InitiativeFacts { LooseItems = true, FiresLow = true, DamagedPieces = true, CanGetHammer = false, BuiltChests = 3, GatheredInPack = true, Animals = true, Trees = true };
        string IdeaTexts(InitiativeFacts f, Func<string, bool> recently) => string.Join("|", Initiative.Ideas(f, recently).ConvertAll(idea => idea.Text));
        string allIdeas = IdeaTexts(facts, key => false);
        Check("ideas: sin martillo no repara", allIdeas.Contains("maintain repair").ToString(), "False");
        Check("ideas: guarda en cofres", allIdeas.Contains("store_items").ToString(), "True");
        Check("ideas: ordena con varios cofres", allIdeas.Contains("organize_chests").ToString(), "True");
        Check("ideas: caza", allIdeas.Contains("cazar").ToString(), "True");
        Check("ideas: sin rocas no hay piedra", allIdeas.Contains("piedra").ToString(), "False");
        Check("ideas: no repite lo reciente", IdeaTexts(facts, key => key == "gather:" + CompanionActions.JobWood).Contains("madera").ToString(), "False");
        Check("ideas: no repite lo que hizo", IdeaTexts(facts, key => key == Initiative.VarietyKey("gather", Parsed("gather", "{\"item\":\"Wood\",\"amount\":5}"))).Contains("madera").ToString(), "False");
        Check("ideas: no repite el mantenimiento", IdeaTexts(facts, key => key == "maintain:PlanFuel").Contains("maintain fuel").ToString(), "False");
        Check("ideas: un solo cofre no se ordena", IdeaTexts(new InitiativeFacts { BuiltChests = 1 }, key => false).Contains("organize_chests").ToString(), "False");
        Check("ideas: cofre encontrado", IdeaTexts(new InitiativeFacts { FoundChest = true }, key => false).Contains("found_chest").ToString(), "True");

        var busyDay = new InitiativeFacts { LooseItems = true, BuiltChests = 3, Animals = true, Trees = true, Rocks = true, Ripe = true, FoundChest = true, FiresLow = true, GatheredInPack = true, CookingSpot = true, CookableDish = "Carne cocinada" };
        foreach (InitiativeFacts danger in new[]
        {
            new InitiativeFacts { Raid = true, Trees = true, LooseItems = true, FiresLow = true },
            new InitiativeFacts { EnemiesClose = true, Trees = true, LooseItems = true, FiresLow = true },
            new InitiativeFacts { PlayerHurt = true, Trees = true, LooseItems = true, FiresLow = true },
            new InitiativeFacts { SelfHurt = true, Trees = true, LooseItems = true, FiresLow = true }
        })
        {
            Check("contexto: con peligro no propone nada", Initiative.Ideas(danger, key => false).Count.ToString(), "0");
            Check("contexto: con peligro lo dice", (Initiative.Context(danger) != null).ToString(), "True");
        }
        var stayTools = new List<string>();
        foreach (JToken tool in Initiative.ToolsFor(new List<InitiativeIdea>(), true)) stayTools.Add((string)tool["function"]["name"]);
        stayTools.Sort();
        Check("contexto: sin moverse, solo comer y equiparse", string.Join("|", stayTools), "consume_item|equip_item");
        Check("contexto: día normal", Initiative.Context(new InitiativeFacts()), null);
        Check("contexto: en el mensaje", Initiative.Message("nada", null, true, null, null, "Hay una incursión.").Contains("Hay una incursión.").ToString(), "True");
        Check("contexto: necesidades primero", Initiative.Message("nada", new[] { "Tienes hambre." }, true).Contains("antes que lo demás").ToString(), "True");

        bool Offers(InitiativeFacts f, string text) => IdeaTexts(f, key => false).Contains(text);
        InitiativeFacts With(Action<InitiativeFacts> change)
        {
            var copy = new InitiativeFacts { LooseItems = true, BuiltChests = 3, Animals = true, Trees = true, Rocks = true, Ripe = true, FoundChest = true, FiresLow = true, GatheredInPack = true, CookingSpot = true, CookableDish = "Carne cocinada" };
            change(copy);
            return copy;
        }
        Check("contexto: de noche fuera no caza", Offers(With(f => f.Night = true), "cazar").ToString(), "False");
        Check("contexto: de noche fuera no tala", Offers(With(f => f.Night = true), "madera").ToString(), "False");
        Check("contexto: de noche fuera no explora", Offers(With(f => f.Night = true), "found_chest").ToString(), "False");
        Check("contexto: de noche en la base sí", Offers(With(f => { f.Night = true; f.AtBase = true; }), "madera").ToString(), "True");
        Check("contexto: mochila llena no recoge más", Offers(With(f => f.PackFull = true), "madera").ToString(), "False");
        Check("contexto: sin hacha no tala", Offers(With(f => f.NoAxe = true), "madera").ToString(), "False");
        Check("contexto: sin pico no pica", Offers(With(f => f.NoPickaxe = true), "piedra").ToString(), "False");
        Check("contexto: sin ingredientes no cocina", Offers(With(f => f.CookableDish = null), "cook_food").ToString(), "False");
        Check("contexto: dice qué cocinar", Offers(busyDay, "Carne cocinada").ToString(), "True");

        var urgentRandom = new Random(3);
        List<InitiativeIdea> nightFires = Initiative.Pick(Initiative.Ideas(With(f => { f.Night = true; f.AtBase = true; }), key => false), 3, urgentRandom);
        Check("urgente: de noche, solo los fuegos", string.Join("|", nightFires.ConvertAll(idea => idea.Text)).Contains("maintain fuel") + "/" + nightFires.Count, "True/1");
        List<InitiativeIdea> fullPack = Initiative.Pick(Initiative.Ideas(With(f => f.PackFull = true), key => false), 3, urgentRandom);
        Check("urgente: mochila llena, a guardar", fullPack.Count + "/" + fullPack[0].Tool, "1/store_items");

        int Chosen(InitiativeFacts f, string text)
        {
            var seeded = new Random(7);
            int times = 0;
            for (int i = 0; i < 2000; i++)
            {
                if (Initiative.Pick(Initiative.Ideas(f, key => false), 1, seeded)[0].Text.Contains(text)) times++;
            }
            return times;
        }
        InitiativeFacts woodOnly = new InitiativeFacts { Trees = true, Rocks = true, Ripe = true };
        int balanced = Chosen(woodOnly, "madera");
        int building = Chosen(new InitiativeFacts { Trees = true, Rocks = true, Ripe = true, PlayerBuilding = true }, "madera");
        int full = Chosen(new InitiativeFacts { Trees = true, Rocks = true, Ripe = true, WoodStored = 500 }, "madera");
        int scarce = Chosen(new InitiativeFacts { Trees = true, Rocks = true, Ripe = true, WoodStored = 3 }, "madera");
        Check("peso: construyendo, más madera", (building > balanced).ToString(), "True");
        Check("peso: cofres llenos, menos madera", (full < balanced / 2).ToString(), "True");
        Check("peso: sin madera en los cofres, más", (scarce > balanced).ToString(), "True");
        Check("peso: aun así hay variedad", (full > 0).ToString(), "True");

        Check("hora: de noche", TimeOfDay.Name(0.1f), "de noche");
        Check("hora: amanece", TimeOfDay.Name(0.25f), "amaneciendo");
        Check("hora: mañana", TimeOfDay.Name(0.4f), "por la mañana");
        Check("hora: tarde", TimeOfDay.Name(0.6f), "por la tarde");
        Check("hora: anochece", TimeOfDay.Name(0.72f), "anocheciendo");
        Check("hora: de noche otra vez", TimeOfDay.Name(0.9f), "de noche");

        var raids = new ReactionPolicy();
        raids.NotifySpoke(100f);
        raids.Add(new Reaction { Kind = ReactionKind.NewBiome, Description = "x" }, 100f);
        raids.Add(new Reaction { Kind = ReactionKind.RaidStarted, Description = "y" }, 100f);
        Check("incursión: se comenta aunque acabe de hablar", raids.TryTake(101f, 60f, 10)?.Kind.ToString(), "RaidStarted");

        var random = new Random(1);
        List<InitiativeIdea> all = Initiative.Ideas(facts, key => false);
        List<InitiativeIdea> picked = Initiative.Pick(all, 3, random);
        Check("elegir: cuántas", picked.Count.ToString(), "3");
        Check("elegir: sin repetir", new HashSet<string>(picked.ConvertAll(idea => idea.Key)).Count.ToString(), "3");
        Check("elegir: pocas disponibles", Initiative.Pick(all.GetRange(0, 2), 3, random).Count.ToString(), "2");
        Check("elegir: ninguna", Initiative.Pick(new List<InitiativeIdea>(), 3, random).Count.ToString(), "0");

        var offered = new Dictionary<string, JToken>();
        foreach (JToken tool in Initiative.ToolsFor(new List<InitiativeIdea> { new InitiativeIdea { Tool = "gather", Job = CompanionActions.JobWood } })) offered[(string)tool["function"]["name"]] = tool;
        Check("ofrecer: lo propuesto", offered.ContainsKey("gather").ToString(), "True");
        Check("ofrecer: lo no propuesto", offered.ContainsKey("organize_chests").ToString(), "False");
        Check("ofrecer: sus necesidades", offered.ContainsKey("consume_item").ToString(), "True");
        var offeredItems = offered["gather"]["function"]["parameters"]["properties"]["item"]["enum"].ToObject<List<string>>();
        Check("ofrecer: solo ese recurso", (offeredItems.Contains("Wood") && !offeredItems.Contains("Stone") && !offeredItems.Contains("Raspberry")).ToString(), "True");
        foreach (JToken tool in Initiative.ToolsFor(new List<InitiativeIdea>())) Check("sin ideas: no sale a por recursos " + tool["function"]["name"], ((string)tool["function"]["name"] != "gather").ToString(), "True");
        Check("mensaje: con ideas", Initiative.Message("nada", null, true, new[] { "idea uno" }).Contains("idea uno").ToString(), "True");
        Check("mensaje: lo reciente", Initiative.Message("nada", null, true, null, new[] { "conseguir piedra" }).Contains("conseguir piedra").ToString(), "True");
        Check("mensaje ocupado: sin ideas", Initiative.Message("nada", null, false, new[] { "idea uno" }).Contains("idea uno").ToString(), "False");
        Check("mensaje: los cofres propios no son un hallazgo", Initiative.Message("nada", null, false).Contains("hallazgo").ToString(), "True");

        // Nombres como los escribe una persona.
        Check("nombres: plural", ItemNames.Matches("Flecha de madera", "flechas").ToString(), "True");
        Check("nombres: entero en plural", ItemNames.Matches("Flecha de madera", "flechas de madera").ToString(), "True");
        Check("nombres: mayúsculas y tildes", ItemNames.Matches("Martillo", "MARTILLOS").ToString(), "True");
        Check("nombres: otra cosa", ItemNames.Matches("Flecha de madera", "piedras").ToString(), "False");
        Check("nombres: vacío", ItemNames.Matches("Flecha", "  ").ToString(), "False");
        foreach (string word in new[] { "flechas", "Flecha", "arrows", "munición", "flechas para el arco", "virotes", "proyectiles" }) Check("munición: " + word, ItemNames.IsAmmoWord(word).ToString(), "True");
        foreach (string word in new[] { "flechas de fuego", "madera", "arco", "" }) Check("no es munición en general: " + word, ItemNames.IsAmmoWord(word).ToString(), "False");

        foreach (string word in new[] { "comida", "Comidas", "food", "algo de comer", "provisiones" }) Check("comida en general: " + word, ItemNames.IsFoodWord(word).ToString(), "True");
        foreach (string word in new[] { "frambuesas", "madera", "comida de perro", "" }) Check("no es comida en general: " + word, ItemNames.IsFoodWord(word).ToString(), "False");

        Check("comida: su parte de 15", FoodPlan.ShareOf(15).ToString(), "3");
        Check("comida: su parte de 11", FoodPlan.ShareOf(11).ToString(), "3");
        Check("comida: su parte de 1", FoodPlan.ShareOf(1).ToString(), "1");
        Check("comida: nada de nada", FoodPlan.ShareOf(0).ToString(), "0");
        FoodStock Food(string item, int inChests, int carried, float value) => new FoodStock { Item = item, InChests = inChests, Carried = carried, Value = value };
        string Plan(int total, params FoodStock[] foods)
        {
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> entry in FoodPlan.Choose(foods, total)) parts.Add(entry.Key + "=" + entry.Value);
            parts.Sort();
            return string.Join(",", parts);
        }
        Check("comida: las tres mejores", Plan(0, Food("carne", 11, 0, 40), Food("frambuesa", 15, 0, 20), Food("seta", 10, 0, 30), Food("baya", 20, 0, 10)), "carne=3,frambuesa=3,seta=2");
        Check("comida: solo hay una", Plan(0, Food("frambuesa", 15, 0, 20)), "frambuesa=3");
        Check("comida: lo que lleva cuenta", Plan(0, Food("frambuesa", 15, 2, 20), Food("carne", 11, 0, 40)), "carne=3,frambuesa=1");
        Check("comida: ya lleva tres", Plan(0, Food("a", 0, 5, 1), Food("b", 0, 5, 1), Food("c", 0, 5, 1), Food("carne", 11, 0, 40)), "");
        Check("comida: lleva de sobra", Plan(0, Food("frambuesa", 15, 9, 20)), "");
        Check("comida: cantidad pedida, repartida", Plan(7, Food("carne", 11, 0, 40), Food("frambuesa", 15, 0, 20), Food("seta", 1, 0, 30)), "carne=3,frambuesa=3,seta=1");
        Check("comida: toda la que se pide", Plan(9999, Food("frambuesa", 15, 0, 20)), "frambuesa=15");

        Check("iniciativa: no se lleva todo un material", (Initiative.Refusal("take_from_chest", Parsed("take_from_chest", "{\"item\":\"madera\"}")) != null).ToString(), "True");
        Check("iniciativa: ni demasiado", (Initiative.Refusal("take_from_chest", Parsed("take_from_chest", "{\"item\":\"madera\",\"amount\":71}")) != null).ToString(), "True");
        Check("iniciativa: coge un poco", Initiative.Refusal("take_from_chest", Parsed("take_from_chest", "{\"item\":\"madera\",\"amount\":10}")), null);
        Check("iniciativa: busca comida", Initiative.Refusal("take_from_chest", Parsed("take_from_chest", "{\"item\":\"comida\"}")), null);
        Check("iniciativa: busca flechas", Initiative.Refusal("take_from_chest", Parsed("take_from_chest", "{\"item\":\"flechas\"}")), null);

        Check("munición: hay en el cofre", GearCheck.AmmoNeed("Arco tosco", "flechas", true, true).Contains("take_from_chest").ToString(), "True");
        Check("munición: no hay, con arma", GearCheck.AmmoNeed("Arco tosco", "flechas", false, true).Contains("cuerpo a cuerpo").ToString(), "True");
        Check("munición: no hay, sin arma", GearCheck.AmmoNeed("Arco tosco", "flechas", false, false).Contains("Tampoco").ToString(), "True");

        Check("mensaje libre: ofrece actuar", Initiative.Message("nada", null, true).Contains("herramienta").ToString(), "True");
        Check("mensaje ocupado: no ofrece actuar", Initiative.Message("nada", null, false).Contains("herramienta").ToString(), "False");
        Check("mensaje: necesidades", Initiative.Message("nada", new[] { "Tienes hambre." }, true).Contains("Tienes hambre.").ToString(), "True");
        Check("callarse", Initiative.StripSilence(" [nada] "), "");
        Check("callarse: nulo", Initiative.StripSilence(null), "");
        Check("callarse: con texto", Initiative.StripSilence("Hola. [nada]"), "Hola.");

        var budget = new InitiativeBudget();
        budget.Reset(0f);
        Check("presupuesto: espera el intervalo", budget.TryTake(100f, 180f, 2).ToString(), "False");
        Check("presupuesto: primera", budget.TryTake(180f, 180f, 2).ToString(), "True");
        budget.NotifySpoke(300f);
        Check("presupuesto: acaba de hablar", budget.TryTake(400f, 180f, 2).ToString(), "False");
        Check("presupuesto: segunda", budget.TryTake(480f, 180f, 2).ToString(), "True");
        Check("presupuesto: límite por hora", budget.TryTake(700f, 180f, 2).ToString(), "False");
        Check("presupuesto: libera la hora", budget.TryTake(3800f, 180f, 2).ToString(), "True");

        Check("modo por defecto en ficha: sin línea", PromptBuilder.Build(new CharacterProfile(), "x", "español", "s").Contains(Initiative.PromptLine).ToString(), "False");
        Check("modo autónomo: con línea", PromptBuilder.Build(new CharacterProfile(), "x", "español", "s", null, false, true).Contains(Initiative.PromptLine).ToString(), "True");

        Check("herramienta escrita 1", LlmProtocol.StripNarration("<tool name=\"gather\" args=\"{}\"/> Voy por ello."), "Voy por ello.");
        Check("herramienta escrita 2", LlmProtocol.StripNarration("<tool_call>gather={\"item\":\"stone\"}</tool_call>"), "");
        Check("texto normal intacto", LlmProtocol.StripNarration("A menos de 5 pasos > mira."), "A menos de 5 pasos > mira.");
    }

    /// <summary>Qué decide por su cuenta en situaciones típicas, con las mismas herramientas que en el juego.</summary>
    private static void LiveInitiative()
    {
        string prompt = PromptBuilder.Build(new CharacterProfile { Instructions = "Eres Saber, una vikinga directa y bromista." }, "", "español",
            "Tu nombre: Saber\nJugador: Leif\nLo que mira el jugador: nada en particular\nTu salud: 60/60\nLo que haces ahora: Siguiéndote\nBioma: Meadows\nMomento: día\nEnemigos cerca: ninguno\nTu inventario: 1 Hacha de piedra\nTus herramientas (hacha: sí; pico: no).",
            null, true, true);
        var cases = new[]
        {
            new { Title = "Libre, con recursos", Message = Initiative.Message("A menos de 40 m: 7 árboles en pie; para recoger: 12 Frambuesas; objetos sueltos: 3 Madera.", null, true), Act = true },
            new { Title = "Libre, con hambre", Message = Initiative.Message("A menos de 40 m: 1 cofre, el más cercano a 8 m al norte.", new[] { "Tienes hambre y no llevas nada de comer." }, true), Act = true },
            new { Title = "Ocupada", Message = Initiative.Message("A menos de 40 m: 3 árboles en pie.", null, false), Act = false },
            new { Title = "Encargo fallido", Message = Initiative.EventMessage(new Reaction { Description = "No has podido terminar el encargo: llevas 8 de 40 de madera porque te falta un hacha." }), Act = true }
        };
        foreach (var scene in cases)
        {
            var sb = new StringBuilder();
            var messages = new List<ChatMessage> { new ChatMessage("system", prompt), new ChatMessage("user", scene.Message) };
            LlmResponse response = Call(messages, scene.Act ? Initiative.Tools() : null, sb);
            Console.WriteLine("== " + scene.Title);
            foreach (ToolCall call in response?.ToolCalls ?? new List<ToolCall>())
            {
                ActionRequest action = CompanionActions.Parse(call.Name, call.Arguments, out string error);
                Console.WriteLine("Acción: " + call.Name + " " + call.Arguments + (action == null ? " RECHAZADA: " + error : Initiative.Refusal(call.Name, action) != null ? " NO PERMITIDA" : ""));
            }
            Console.WriteLine("Dice: " + Initiative.StripSilence(response?.Text) + "\n");
        }

        // La orden que originó el cambio: ¿usa la herramienta nueva?
        string orders = PromptBuilder.Build(new CharacterProfile { Instructions = "Eres Saber, una vikinga directa y bromista." }, "", "español",
            "Tu nombre: Saber\nJugador: Leif\nLo que mira el jugador: Hacha de piedra\nLo que haces ahora: Siguiéndote\nBioma: Meadows");
        foreach (string order in new[] { "recoge el hacha", "coge eso del suelo" })
        {
            var sb = new StringBuilder();
            LlmResponse response = Call(new List<ChatMessage> { new ChatMessage("system", orders), new ChatMessage("user", order) }, CompanionActions.ToolDefinitions(), sb);
            string calls = "";
            foreach (ToolCall call in response?.ToolCalls ?? new List<ToolCall>()) calls += call.Name + " " + call.Arguments + "; ";
            Console.WriteLine("Tú: " + order + " -> " + calls);
        }
    }

    private static void CheckFirstPlaytest()
    {
        // Nada de una respuesta larga se pierde al repartirla.
        const string longReply = "Primera frase bastante larga para el tramo. Segunda frase también larga para el tramo. Tercera frase final.";
        List<string> pages = LlmProtocol.Pages(longReply, 50);
        Check("tramos: número", pages.Count.ToString(), "3");
        Check("tramos: corta por frase", pages[0], "Primera frase bastante larga para el tramo.");
        Check("tramos: no pierde nada", string.Join(" ", pages), longReply);
        Check("tramos: lo corto queda igual", string.Join("|", LlmProtocol.Pages("  Hola,\n amigo.  ", 50)), "Hola, amigo.");
        Check("tramos: sin puntos corta por palabra", string.Join("|", LlmProtocol.Pages("palabra palabra palabra palabra", 18)), "palabra palabra...|palabra palabra");
        Check("tramos: palabra enorme", string.Join("|", LlmProtocol.Pages("abcdefghij", 4)), "abcd...|efgh...|ij");
        Check("tramos: vacío", LlmProtocol.Pages("  ", 50).Count.ToString(), "0");
        Check("tramos: sin límite", LlmProtocol.Pages(longReply, 0).Count.ToString(), "1");

        string error;
        foreach (string place in new[] { "chest", "bed", "station", "fire", "ship", "tree", "rock" })
        {
            Check("ir a " + place, CompanionActions.Parse("go_to", "{\"place\":\"" + place + "\"}", out error)?.Kind.ToString(), "GoTo");
        }
        Check("ir al cofre: se ve en el chat", CompanionActions.Parse("go_to", "{\"place\":\"chest\"}", out error)?.Description, "ir al cofre más cercano");
        Check("ir al cofre: valor", CompanionActions.Parse("go_to", "{\"place\":\"chest\"}", out error)?.Value.ToString(), CompanionActions.PlaceChest.ToString());
        Check("proteger no acepta el cofre", (CompanionActions.Parse("guard_area", "{\"place\":\"chest\"}", out error) == null).ToString(), "True");

        Check("brújula: norte", Compass.Direction(0f, 10f), "norte");
        Check("brújula: este", Compass.Direction(10f, 0f), "este");
        Check("brújula: sur", Compass.Direction(0f, -10f), "sur");
        Check("brújula: oeste", Compass.Direction(-10f, 0f), "oeste");
        Check("brújula: noreste", Compass.Direction(5f, 5f), "noreste");
        Check("brújula: noroeste", Compass.Direction(-5f, 5f), "noroeste");

        var names = new[] { "Darkness", "Bjorn" };
        Check("chat solo: todo cuenta", Recipients.GameChatIsForCompanions("ven aquí", names, false).ToString(), "True");
        Check("chat con gente: sin nombre no", Recipients.GameChatIsForCompanions("¿vamos a la cueva?", names, true).ToString(), "False");
        Check("chat con gente: con nombre sí", Recipients.GameChatIsForCompanions("darkness, ven aquí", names, true).ToString(), "True");

        string shout;
        Check("grito: es grito", Recipients.TryParseShout("/s ven aquí", out shout).ToString(), "True");
        Check("grito: el mensaje", shout, "ven aquí");
        Check("grito: mayúsculas y espacios", Recipients.TryParseShout("  /S   ven  ", out shout) + "|" + shout, "True|ven");
        Check("grito: sin mensaje", Recipients.TryParseShout("/s ", out shout).ToString(), "False");
        Check("grito: otro comando", Recipients.TryParseShout("/say hola", out shout).ToString(), "False");
        Check("grito: comando del mod", Recipients.TryParseShout("/saldo", out shout).ToString(), "False");
        Check("grito: mensaje normal", Recipients.TryParseShout("hola", out shout).ToString(), "False");

        ToolCall nameLast = OrderIntent.Detect("ven darkness", new[] { "Darkness" });
        Check("orden clara: nombre al final", nameLast?.Name, "come_here");
        Check("orden clara: a quién, nombre al final", (string)JObject.Parse(nameLast.Arguments)["who"], "Darkness");
        Check("orden clara: nombre al principio", OrderIntent.Detect("darkness, ven", new[] { "Darkness" })?.Name, "come_here");
        Check("orden clara: otra frase con el nombre", (OrderIntent.Detect("hola darkness", new[] { "Darkness" }) == null).ToString(), "True");

        string prompt = PromptBuilder.Build(new CharacterProfile(), "x", "español", "s");
        Check("trato de igual a igual", prompt.Contains("de igual a igual").ToString(), "True");
        Check("la ficha puede pedir otro trato", prompt.Contains("salvo que tu ficha pida ese trato").ToString(), "True");
    }

    private static void CheckEmotes()
    {
        string trigger;
        Check("gesto: texto limpio", EmoteCatalog.Extract("[gesto:beso] Te quiero, bobo.", out trigger), "Te quiero, bobo.");
        Check("gesto: nombre del juego", trigger, "blowkiss");
        EmoteCatalog.Extract("[Gesto: Señalar] Mira.", out trigger);
        Check("gesto: acentos y mayúsculas", trigger, "point");
        Check("gesto: al final", EmoteCatalog.Extract("Ja, buena. (gesto:reir)", out trigger), "Ja, buena.");
        Check("gesto: al final, nombre", trigger, "laugh");
        Check("gesto: inventado no se ve", EmoteCatalog.Extract("[gesto:volar] Mira.", out trigger), "Mira.");
        Check("gesto: inventado no se hace", (trigger == null).ToString(), "True");
        EmoteCatalog.Extract("[gesto:sentarse] Hola.", out trigger);
        Check("gesto: sentarse no está", (trigger == null).ToString(), "True");
        Check("gesto: varios, sin etiquetas", EmoteCatalog.Extract("[gesto:reir] Sí. [gesto:bailar] Vamos.", out trigger), "Sí. Vamos.");
        Check("gesto: varios, el primero", trigger, "laugh");
        Check("gesto: solo etiqueta", EmoteCatalog.Extract("[gesto:saludar]", out trigger), "");
        Check("gesto: sin etiqueta", EmoteCatalog.Extract("Hola, [amigo].", out trigger), "Hola, [amigo].");
        Check("gesto: sin etiqueta, nada", (trigger == null).ToString(), "True");
        Check("gesto: sin texto", (EmoteCatalog.Extract(null, out trigger) == null).ToString(), "True");

        Check("gesto: se ofrece si puede", PromptBuilder.Build(new CharacterProfile(), "", "español", "", null, true).Contains("[gesto:").ToString(), "True");
        Check("gesto: no se ofrece si no puede", PromptBuilder.Build(new CharacterProfile(), "", "español", "").Contains("[gesto:").ToString(), "False");

        var goal = new EmoteGoal(0f);
        Check("gesto: espera a que empiece", goal.Finished(false, 1f).ToString(), "False");
        Check("gesto: sigue mientras dura", goal.Finished(true, 1.5f).ToString(), "False");
        Check("gesto: acaba con la animación", goal.Finished(false, 3f).ToString(), "True");
        Check("gesto: empezó", goal.Entered.ToString(), "True");
        Check("gesto: bailar tiene máximo", new EmoteGoal(0f).Finished(true, EmoteGoal.MaxSeconds).ToString(), "True");
        var never = new EmoteGoal(0f);
        Check("gesto: si nunca empieza, acaba", never.Finished(false, EmoteGoal.EnterSeconds).ToString(), "True");
        Check("gesto: nunca empezó", never.Entered.ToString(), "False");
    }

    private static void CheckRolePlayNotes()
    {
        Check("avisos: pico solo en el chat", CompanionActions.ReplyNeedsNote(ActionKind.Gather).ToString(), "False");
        Check("avisos: trabajo solo en el chat", CompanionActions.ReplyNeedsNote(ActionKind.SetJob).ToString(), "False");
        Check("avisos: fundir solo en el chat", CompanionActions.ReplyNeedsNote(ActionKind.SmeltItem).ToString(), "False");
        Check("avisos: proteger solo en el chat", CompanionActions.ReplyNeedsNote(ActionKind.Guard).ToString(), "False");
        Check("datos: cofres para la respuesta", CompanionActions.ReplyNeedsNote(ActionKind.CheckChests).ToString(), "True");
        Check("datos: habilidades para la respuesta", CompanionActions.ReplyNeedsNote(ActionKind.CheckSkills).ToString(), "True");
        Check("datos: llamada para la respuesta", CompanionActions.ReplyNeedsNote(ActionKind.RecallAll).ToString(), "True");
        string prompt = PromptBuilder.Build(new CharacterProfile(), "", "español", "");
        Check("rol: no recita límites", prompt.Contains("No expliques cómo harás algo").ToString(), "True");
        Check("rol: no promete de más", prompt.Contains("no prometas").ToString(), "False");
    }

    private static void CheckBubble()
    {
        Check("burbuja: tapa el nombre, sube lo justo", BubbleLayout.Lift(480f, 600f, 500f, 10f, 1080f).ToString(), "30");
        Check("burbuja: ya está por encima, no se mueve", BubbleLayout.Lift(520f, 600f, 500f, 10f, 1080f).ToString(), "0");
        Check("burbuja: justo en el margen, no se mueve", BubbleLayout.Lift(510f, 600f, 500f, 10f, 1080f).ToString(), "0");
        Check("burbuja: no se sale por arriba", BubbleLayout.Lift(480f, 1070f, 500f, 10f, 1080f).ToString(), "10");
        Check("burbuja: ya tocando arriba, no sube", BubbleLayout.Lift(480f, 1090f, 500f, 10f, 1080f).ToString(), "0");
    }

    private static void CheckGroupOne()
    {
        string error;
        Check("lista: tipo", CompanionActions.Parse("companions_status", "{}", out error)?.Kind.ToString(), "CompanionsStatus");
        Check("llamada: tipo", CompanionActions.Parse("recall_all", "{}", out error)?.Kind.ToString(), "RecallAll");
        Check("llamada: inmediata", CompanionActions.IsExclusiveTask(ActionKind.RecallAll).ToString(), "False");
        Check("lista: no depende de quién esté cerca", CompanionActions.IsWorldWide(ActionKind.CompanionsStatus).ToString(), "True");
        Check("llamada: no depende de quién esté cerca", CompanionActions.IsWorldWide(ActionKind.RecallAll).ToString(), "True");
        Check("habilidades: sí depende de quién esté cerca", CompanionActions.IsWorldWide(ActionKind.CheckSkills).ToString(), "False");
        Check("martillo: reparar", CompanionActions.NeedsHammer(CompanionActions.PlanForTask("repair")).ToString(), "True");
        Check("martillo: pilas", CompanionActions.NeedsHammer(CompanionActions.PlanForTask("piles")).ToString(), "True");
        Check("martillo: fuegos", CompanionActions.NeedsHammer(CompanionActions.PlanForTask("fuel")).ToString(), "False");
        Check("martillo: miel", CompanionActions.NeedsHammer(CompanionActions.PlanForTask("honey")).ToString(), "False");
        Check("martillo: cofres", CompanionActions.NeedsHammer(CompanionActions.SortPlan).ToString(), "False");
        ActionRequest fuelOrder = CompanionActions.Parse("maintain", "{\"task\":\"fuel\"}", out error);
        Check("hornos: rellenar no es reavivar", (CompanionActions.FurnaceMixup(fuelOrder, "¿Puedes rellenar los Hornos?") != null).ToString(), "True");
        Check("hornos: la corrección lleva al trabajo de fundir", CompanionActions.FurnaceMixup(fuelOrder, "carga el horno de carbón").Contains("set_job con smelt").ToString(), "True");
        Check("hornos: fuegos nombrados, se respeta", (CompanionActions.FurnaceMixup(fuelOrder, "reaviva el fuego junto al horno") == null).ToString(), "True");
        Check("hornos: sin hornos, se respeta", (CompanionActions.FurnaceMixup(fuelOrder, "reaviva todo, que se apaga") == null).ToString(), "True");
        Check("hornos: por su cuenta no hay texto del jugador", (CompanionActions.FurnaceMixup(fuelOrder, null) == null).ToString(), "True");
        Check("hornos: otras tareas no se tocan", (CompanionActions.FurnaceMixup(CompanionActions.Parse("maintain", "{\"task\":\"repair\"}", out error), "repara el horno") == null).ToString(), "True");
        foreach (string chosen in new[] { "FineWood", "RoundLog", "ElderBark", "YggdrasilWood", "Blackwood", "Resin" })
        {
            Check("talar: " + chosen + " solo de los árboles que la dan", CompanionActions.NeedsChosenTrees(chosen).ToString(), "True");
        }
        Check("talar: la madera normal sale de cualquiera", CompanionActions.NeedsChosenTrees("Wood").ToString(), "False");
        Check("talar: la piedra no es de talar", CompanionActions.NeedsChosenTrees("Stone").ToString(), "False");
        Check("talar: sin objeto", CompanionActions.NeedsChosenTrees(null).ToString(), "False");
        Check("habilidades: who", CompanionActions.Parse("check_skills", "{\"who\":\"all\"}", out error)?.Who, "all");
        Check("hogar: tamaño", CompanionActions.Parse("set_home_radius", "{\"meters\":80}", out error)?.Value.ToString(), "80");
        Check("hogar: pequeño", (CompanionActions.Parse("set_home_radius", "{\"meters\":1}", out error) == null).ToString(), "True");
        Check("hogar: grande", (CompanionActions.Parse("set_home_radius", "{\"meters\":900}", out error) == null).ToString(), "True");
        Check("hogar: sin dato", (CompanionActions.Parse("set_home_radius", "{}", out error) == null).ToString(), "True");
        Check("fundir: material", CompanionActions.Parse("smelt_item", "{\"item\":\"cobre\"}", out error)?.Item, "cobre");
        Check("fundir: en cadena", CompanionActions.IsExclusiveTask(ActionKind.SmeltItem).ToString(), "True");
        Check("fundir: sin material", (CompanionActions.Parse("smelt_item", "{}", out error) == null).ToString(), "True");
        Check("mantener: fuegos", CompanionActions.Parse("maintain", "{\"task\":\"fuel\"}", out error)?.Item, "PlanFuel");
        Check("mantener: reparar", CompanionActions.Parse("maintain", "{\"task\":\"repair\"}", out error)?.Item, "PlanRepair");
        Check("mantener: miel", CompanionActions.Parse("maintain", "{\"task\":\"honey\"}", out error)?.Item, "PlanHoney");
        Check("mantener: pilas", CompanionActions.Parse("maintain", "{\"task\":\"piles\"}", out error)?.Item, "PlanPiles");
        Check("mantener: inventada", (CompanionActions.Parse("maintain", "{\"task\":\"despawn\"}", out error) == null).ToString(), "True");
        Check("mantener: en cadena", CompanionActions.IsExclusiveTask(ActionKind.Maintain).ToString(), "True");

        var watch = new ProgressWatch(10, 0f);
        Check("progreso: sin cambio", watch.SecondsWithoutChange(10, 5f).ToString(), "5");
        Check("progreso: cambio reinicia", watch.SecondsWithoutChange(9, 7f).ToString(), "0");
        Check("progreso: cuenta desde el cambio", watch.SecondsWithoutChange(9, 10f).ToString(), "3");
        watch.Postpone(3f);
        Check("progreso: pelear no cuenta", watch.SecondsWithoutChange(9, 10f).ToString(), "0");
    }

    private static void CheckVersionComplete()
    {
        string error;
        Check("recordar: tipo", CompanionActions.Parse("remember_fact", "{\"fact\":\"La base está al norte\"}", out error)?.Kind.ToString(), "Remember");
        Check("recordar: vacío", (CompanionActions.Parse("remember_fact", "{\"fact\":\"  \"}", out error) == null).ToString(), "True");
        Check("recordar: inmediato", CompanionActions.IsExclusiveTask(ActionKind.Remember).ToString(), "False");
        Check("mirar: tipo", CompanionActions.Parse("look_around", "{}", out error)?.Kind.ToString(), "LookAround");
        Check("mirar: inmediato", CompanionActions.IsExclusiveTask(ActionKind.LookAround).ToString(), "False");

        var facts = new CompanionMemory { Summary = "Le gusta la cerveza." };
        int revision = facts.Revision;
        Check("hecho: se añade", facts.RememberFact("La base está al norte").ToString(), "True");
        Check("hecho: aparte del resumen", facts.Summary + "|" + facts.Facts[0], "Le gusta la cerveza.|La base está al norte");
        Check("hecho: no anula el resumen en curso", (facts.Revision == revision).ToString(), "True");
        Check("hecho: repetido", facts.RememberFact("la base está al norte").ToString(), "False");
        Check("hecho: vacío", facts.RememberFact("  ").ToString(), "False");
        Check("hecho: sin recuerdos previos", new CompanionMemory().RememberFact("Algo") .ToString(), "True");
        var full = new CompanionMemory();
        for (int i = 0; i < 10; i++) full.RememberFact(i + new string('a', CompanionMemory.MaxFactLength - 1));
        Check("hecho: no cabe más", (full.Facts.Count * CompanionMemory.MaxFactLength <= CompanionMemory.MaxFactsLength).ToString(), "True");
        Check("hecho: rechaza lo que no cabe", full.RememberFact(new string('z', CompanionMemory.MaxFactLength)).ToString(), "False");
        Check("hecho: recorta lo largo", (new CompanionMemory().RememberFact(new string('b', 500))).ToString(), "True");
        Check("necesidad: reparar con estación", GearCheck.RepairNeed(true).Contains("repair_gear").ToString(), "True");
        Check("necesidad: reparar sin estación", GearCheck.RepairNeed(false).Contains("repair_gear").ToString(), "False");
        Check("necesidad: reparar no pide materiales", GearCheck.RepairNeed(false).Contains("sin gastar materiales").ToString(), "True");
        string offered = Initiative.ToolsFor(new List<InitiativeIdea>(), false, false).ToString();
        Check("iniciativa: sin estación no ofrece reparar", offered.Contains("repair_gear").ToString(), "False");
        Check("iniciativa: con estación ofrece reparar", Initiative.ToolsFor(new List<InitiativeIdea>(), false, true).ToString().Contains("repair_gear").ToString(), "True");
        Check("frases: avisos deja pasar problemas", RebornSpeech.Allows(RebornSpeechMode.Avisos, RebornSpeech.Problem).ToString(), "True");
        Check("frases: avisos deja pasar sucesos", RebornSpeech.Allows(RebornSpeechMode.Avisos, RebornSpeech.Event).ToString(), "True");
        Check("frases: avisos calla la charla", RebornSpeech.Allows(RebornSpeechMode.Avisos, RebornSpeech.Ambient).ToString(), "False");
        Check("frases: avisos calla el como ordenes", RebornSpeech.Allows(RebornSpeechMode.Avisos, RebornSpeech.Order).ToString(), "False");
        Check("frases: todas", RebornSpeech.Allows(RebornSpeechMode.Todas, RebornSpeech.Ambient).ToString(), "True");
        Check("frases: ninguna", RebornSpeech.Allows(RebornSpeechMode.Ninguna, RebornSpeech.Problem).ToString(), "False");
        Check("perfil: siempre el mismo", (PersonalityPresets.For("abc") == PersonalityPresets.For("abc")).ToString(), "True");
        var presetsSeen = new HashSet<string>();
        for (int i = 0; i < 200; i++) presetsSeen.Add(PersonalityPresets.For("companion" + i).Name);
        Check("perfil: se reparten todos", presetsSeen.Count.ToString(), PersonalityPresets.All.Length.ToString());
        Check("perfil: vale con cualquier id", (PersonalityPresets.For(new string('z', 500)) != null && PersonalityPresets.For(null) != null).ToString(), "True");
        Check("perfil: el valor antiguo no cuenta como elegido", (PersonalityPresets.DefaultFor(PersonalityPresets.OldDefault, "abc") == PersonalityPresets.For("abc").Text).ToString(), "True");
        Check("perfil: vacío usa el suyo", (PersonalityPresets.DefaultFor("  ", "abc") == PersonalityPresets.For("abc").Text).ToString(), "True");
        Check("perfil: respeta la común escrita", PersonalityPresets.DefaultFor("Eres un pirata.", "abc"), "Eres un pirata.");

        Check("armadura: golpe grande", ThreatAssessment.AfterArmor(30f, 10f).ToString("F1"), "20.0");
        Check("armadura: golpe pequeño", ThreatAssessment.AfterArmor(18f, 10f).ToString("F1"), "8.1");
        Check("armadura: sin armadura", ThreatAssessment.AfterArmor(18f, 0f).ToString("F1"), "18.0");
        Check("armadura: sin golpe", ThreatAssessment.AfterArmor(0f, 0f).ToString("F1"), "0.0");
        Check("amenaza: las estrellas pegan más", ThreatAssessment.HitOn(20f, 3, 0f).ToString("F1"), "40.0");
        // Salud y daño de la wiki (valheim.fandom.com/wiki/Creatures), sin contar el daño de hacha y pico, que no hiere.
        // Equipo inicial supuesto: 100 de salud, 10 de armadura y 30 de golpe, con el jugador al lado.
        Check("amenaza: estrellas como en la wiki", ThreatAssessment.HitOn(70f, 2, 0f).ToString("F0") + "/" + ThreatAssessment.HitOn(70f, 3, 0f).ToString("F0"), "105/140");
        Check("amenaza: un lobo", EarlyGameAvoids(80f, 70f, 1).ToString(), "True");
        Check("amenaza: un lobo aunque sean 3", EarlyGameAvoids(80f, 70f, 1, 3).ToString(), "True");
        Check("amenaza: un lobo aunque sean 4", EarlyGameAvoids(80f, 70f, 1, 4).ToString(), "True");
        Check("amenaza: tres lobos aunque sean 4", EarlyGameAvoids(80f, 70f, 3, 4).ToString(), "True");
        Check("amenaza: oso aunque sean 4", EarlyGameAvoids(800f, 55f, 1, 4).ToString(), "True");
        Check("amenaza: tres enanos grises siendo 4", EarlyGameAvoids(40f, 14f, 3, 4).ToString(), "False");
        Check("amenaza: a solas, tres enanos grises", EarlyGameAvoids(40f, 14f, 3, 1).ToString(), "False");
        Check("amenaza: a solas, un esqueleto", EarlyGameAvoids(40f, 25f, 1, 1).ToString(), "False");
        Check("amenaza: tres lobos", EarlyGameAvoids(80f, 70f, 3).ToString(), "True");
        Check("amenaza: oso", EarlyGameAvoids(800f, 55f, 1).ToString(), "True");
        Check("amenaza: trol", EarlyGameAvoids(600f, 70f, 1).ToString(), "True");
        Check("amenaza: tres draugr", EarlyGameAvoids(100f, 48f, 3).ToString(), "True");
        Check("amenaza: un draugr", EarlyGameAvoids(100f, 48f, 1).ToString(), "False");
        Check("amenaza: jabalí", EarlyGameAvoids(10f, 10f, 1).ToString(), "False");
        Check("amenaza: tres enanos grises", EarlyGameAvoids(40f, 14f, 3).ToString(), "False");
        Check("amenaza: enano bruto", EarlyGameAvoids(150f, 30f, 1).ToString(), "False");
        Check("amenaza: tres esqueletos", EarlyGameAvoids(40f, 25f, 3).ToString(), "False");
        Check("amenaza: tres lobos con hierro", ThreatAssessment.TooStrong(240f, ThreatAssessment.HitOn(70f, 1, 45f) * 3, ThreatAssessment.HitOn(70f, 1, 45f), 150f, 70f, 2, ThreatAssessment.DefaultLimit).ToString(), "False");
        Check("amenaza: golpe desconocido", ThreatAssessment.TooStrong(800f, 0f, 0f, 100f, 30f, 2, ThreatAssessment.DefaultLimit).ToString(), "False");
        Check("amenaza: enemigo que cae enseguida", ThreatAssessment.TooStrong(10f, 90f, 90f, 100f, 30f, 2, ThreatAssessment.DefaultLimit).ToString(), "False");
        float hitWolf = ThreatAssessment.HitOn(70f, 1, 10f);
        Check("amenaza: a solas es peor", (ThreatAssessment.Ratio(160f, hitWolf * 2, 100f, 30f, 1) > ThreatAssessment.Ratio(160f, hitWolf * 2, 100f, 30f, 2)).ToString(), "True");
        Check("amenaza: sin arma", float.IsInfinity(ThreatAssessment.Ratio(80f, 20f, 100f, 0f, 1)).ToString(), "False");
        Check("amenaza: el aviso no espera", ReactsAtOnce(ReactionKind.StrongEnemy).ToString(), "True");

        Check("saldo: leído", LlmProtocol.ParseJoules("{\"joules\":834,\"patron_tier\":\"x\"}").ToString(), "834");
        Check("saldo: sin dato", LlmProtocol.ParseJoules("{}").HasValue.ToString(), "False");
        Check("saldo: roto", LlmProtocol.ParseJoules("<html>").HasValue.ToString(), "False");

        Check("nombre: válido", CompanionActions.Parse("rename_companion", "{\"name\":\" Grimhild \"}", out error)?.Item, "Grimhild");
        Check("nombre: con acento", CompanionActions.Parse("rename_companion", "{\"name\":\"Óðinn\"}", out error)?.Item, "Óðinn");
        Check("nombre: vacío", (CompanionActions.Parse("rename_companion", "{\"name\":\" \"}", out error) == null).ToString(), "True");
        Check("nombre: etiquetas", (CompanionActions.Parse("rename_companion", "{\"name\":\"<color=red>X\"}", out error) == null).ToString(), "True");
        Check("nombre: largo", (CompanionActions.Parse("rename_companion", "{\"name\":\"" + new string('a', 40) + "\"}", out error) == null).ToString(), "True");
        Check("nombre: no es tarea", CompanionActions.IsExclusiveTask(ActionKind.Rename).ToString(), "False");

        var names = new[] { "Saber" };
        Check("intención: quédate", OrderIntent.Detect("Quédate aquí", names)?.Name, "set_follow");
        Check("intención: quédate argumentos", OrderIntent.Detect("Quédate aquí", names)?.Arguments, "{\"follow\":false}");
        Check("intención: todos quietos", OrderIntent.Detect("Todos, quédense aquí", names)?.Arguments, "{\"follow\":false,\"who\":\"all\"}");
        Check("intención: frase con quedar", OrderIntent.Detect("Quédate aquí y tala madera", names)?.Name, null);

        var m = new CompanionMemory();
        var gained = new Dictionary<string, int> { { "Wood", 3 } };
        Check("estado: primer guardado", m.UpdateState(40, true, 1f, 2f, 3f, "talar", gained).ToString(), "True");
        Check("estado: sin cambios", m.UpdateState(40, true, 1f, 2f, 3f, "talar", new Dictionary<string, int> { { "Wood", 3 } }).ToString(), "False");
        Check("estado: cambia retirada", m.UpdateState(0, true, 1f, 2f, 3f, "talar", gained).ToString(), "True");
        Check("estado: cambia lo ganado", m.UpdateState(0, true, 1f, 2f, 3f, "talar", new Dictionary<string, int> { { "Wood", 4 } }).ToString(), "True");
        Check("estado: sin lo ganado", m.UpdateState(0, true, 1f, 2f, 3f, "talar", null).ToString(), "True");
        Check("estado: posición sin proteger da igual", m.UpdateState(0, false, 0f, 0f, 0f, "talar", null).ToString(), "True");
        Check("estado: sin proteger estable", m.UpdateState(0, false, 9f, 9f, 9f, "talar", null).ToString(), "False");

        string folder = Path.Combine(Path.GetTempPath(), "ocai_state_" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonFileStore<CompanionMemory>(folder);
            var saved = new CompanionMemory();
            saved.UpdateState(50, true, 1.5f, 2f, 3f, "talar", new Dictionary<string, int> { { "Wood", 7 } });
            store.Save("x", saved);
            CompanionMemory loaded = store.Load("x", out string problem);
            Check("estado: guarda retirada", loaded.RetreatPercent.ToString(), "50");
            Check("estado: guarda proteger", loaded.GuardActive + "/" + loaded.GuardX, "True/1.5");
            Check("estado: guarda tarea", loaded.LastTaskDescription + loaded.LastTaskGained["Wood"], "talar7");

            File.WriteAllText(store.PathFor("viejo"), "{\"Summary\":\"s\",\"Recent\":[]}");
            CompanionMemory old = store.Load("viejo", out problem);
            Check("estado: archivo antiguo", old.Summary + old.RetreatPercent + old.LastTaskGained.Count, "s00");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    private static void CheckIntent()
    {
        var names = new[] { "Saber", "Bjorn" };
        Check("intención: sígueme", OrderIntent.Detect("Sígueme", names)?.Name, "come_here");
        Check("intención: con signos", OrderIntent.Detect("¡Sígueme!", names)?.Name, "come_here");
        Check("intención: ven aquí", OrderIntent.Detect("Ven aquí.", names)?.Name, "come_here");
        Check("intención: para", OrderIntent.Detect("Detente", names)?.Name, "cancel_orders");
        Check("intención: por nombre", OrderIntent.Detect("Bjorn, sígueme", names)?.Arguments, "{\"who\":\"Bjorn\"}");
        Check("intención: todos", OrderIntent.Detect("Todos, síganme", names)?.Arguments, "{\"who\":\"all\"}");
        Check("intención: todos ven", OrderIntent.Detect("Todos, vengan", names)?.Arguments, "{\"who\":\"all\"}");
        Check("intención: frase normal", OrderIntent.Detect("¿Vamos a talar?", names)?.Name, null);
        Check("intención: pregunta", OrderIntent.Detect("¿Para qué sirve esto?", names)?.Name, null);
        Check("intención: me sigues", OrderIntent.Detect("Me sigues gustando", names)?.Name, null);
        Check("intención: vacío", OrderIntent.Detect("", names)?.Name, null);
        Check("intención: vamos solo", OrderIntent.Detect("Vamos", names)?.Name, "come_here");
    }

    private static void CheckGiving()
    {
        var before = new Dictionary<string, int> { { "Wood", 20 }, { "Stone", 5 } };
        var after = new Dictionary<string, int> { { "Wood", 23 }, { "Stone", 2 }, { "Resin", 1 } };
        Dictionary<string, int> gained = InventoryDiff.Gained(before, after);
        Check("diferencia: suma", gained["Wood"].ToString(), "3");
        Check("diferencia: nuevo", gained["Resin"].ToString(), "1");
        Check("diferencia: lo perdido no cuenta", gained.ContainsKey("Stone").ToString(), "False");
        Check("diferencia: nada", InventoryDiff.Gained(before, before).Count.ToString(), "0");

        var give = new GiveGoal(0f);
        Check("entrega: lejos", give.InReach(5f).ToString(), "False");
        Check("entrega: cerca", give.InReach(GiveGoal.HandDistance).ToString(), "True");
        Check("entrega: camina al empezar", give.ShouldWalk(0f).ToString(), "True");
        Check("entrega: no repite enseguida", give.ShouldWalk(1f).ToString(), "False");
        Check("entrega: repite si te mueves", give.ShouldWalk(GiveGoal.RewalkSeconds).ToString(), "True");
        Check("entrega: tiempo agotado", give.TimedOut(GiveGoal.TimeoutSeconds).ToString(), "True");

        var pausedGive = new GiveGoal(0f);
        pausedGive.Postpone(20f);
        Check("pausa: entrega sin agotarse", pausedGive.TimedOut(GiveGoal.TimeoutSeconds).ToString(), "False");
        Check("pausa: entrega se agota después", pausedGive.TimedOut(GiveGoal.TimeoutSeconds + 20f).ToString(), "True");

        var pausedPlant = new PlantGoal(0f);
        pausedPlant.Next(true, 1f, 1f);
        pausedPlant.ReleaseReach();
        Check("pausa: planta suelta la IA", pausedPlant.InReach.ToString(), "False");
        pausedPlant.Postpone(20f);
        Check("pausa: planta sin agotarse", pausedPlant.Next(true, 9f, PlantGoal.TimeoutSeconds).ToString(), "Wait");

        var pausedTree = new TreeGoal(0f);
        pausedTree.Postpone(60f);
        Check("pausa: árbol sin agotarse", pausedTree.Update(true, 0, TreeGoal.TimeoutSeconds).ToString(), "Working");

        string error;
        Check("comer: tipo", CompanionActions.Parse("consume_item", "{\"item\":\"food\"}", out error)?.Kind.ToString(), "Consume");
        Check("comer: no es tarea", CompanionActions.IsExclusiveTask(ActionKind.Consume).ToString(), "False");
        Check("comer: sin objeto", (CompanionActions.Parse("consume_item", "{}", out error) == null).ToString(), "True");
        Check("equipar: tipo", CompanionActions.Parse("equip_item", "{\"item\":\"Arco\"}", out error)?.Kind.ToString(), "Equip");
        Check("equipar: sin objeto", (CompanionActions.Parse("equip_item", "{\"item\":\"\"}", out error) == null).ToString(), "True");
        ActionRequest chest = CompanionActions.Parse("take_from_chest", "{\"item\":\"Madera\",\"amount\":4}", out error);
        Check("cofre: tipo", chest?.Kind.ToString(), "TakeFromChest");
        Check("cofre: cantidad", chest?.Amount.ToString(), "4");
        Check("cofre: va en cadena", CompanionActions.IsExclusiveTask(ActionKind.TakeFromChest).ToString(), "True");
        Check("cofre: cantidad mala", (CompanionActions.Parse("take_from_chest", "{\"item\":\"Madera\",\"amount\":0}", out error) == null).ToString(), "True");
        Check("proteger: aquí", CompanionActions.Parse("guard_area", "{\"place\":\"here\"}", out error)?.Kind.ToString(), "Guard");
        Check("proteger: allí", CompanionActions.Parse("guard_area", "{\"place\":\"looked_at\"}", out error)?.Value.ToString(), CompanionActions.PlaceLookedAt.ToString());
        Check("proteger: lugar inventado", (CompanionActions.Parse("guard_area", "{\"place\":\"casa\"}", out error) == null).ToString(), "True");
        Check("retirada: valor", CompanionActions.Parse("set_retreat", "{\"health_percent\":40}", out error)?.Value.ToString(), "40");
        Check("retirada: desactivar", CompanionActions.Parse("set_retreat", "{\"health_percent\":0}", out error)?.Value.ToString(), "0");
        Check("retirada: bajo", (CompanionActions.Parse("set_retreat", "{\"health_percent\":5}", out error) == null).ToString(), "True");
        Check("retirada: alto", (CompanionActions.Parse("set_retreat", "{\"health_percent\":95}", out error) == null).ToString(), "True");
        Check("retirada: sin valor", (CompanionActions.Parse("set_retreat", "{}", out error) == null).ToString(), "True");
        Check("retirada: no es tarea", CompanionActions.IsExclusiveTask(ActionKind.SetRetreat).ToString(), "False");

        ActionRequest drop = CompanionActions.Parse("drop_items", "{\"item\":\"madera\",\"amount\":2}", out error);
        Check("soltar: tipo", drop?.Kind.ToString(), "DropItems");
        Check("soltar: cantidad", drop?.Amount.ToString(), "2");
        Check("soltar: en cadena", CompanionActions.IsExclusiveTask(ActionKind.DropItems).ToString(), "True");
        Check("soltar: última tarea", CompanionActions.Parse("drop_items", "{\"item\":\"last_task\"}", out error)?.Item, CompanionActions.LastTaskItems);
        Check("soltar: sin objeto", (CompanionActions.Parse("drop_items", "{}", out error) == null).ToString(), "True");
        Check("dar sigue siendo dar", CompanionActions.Parse("give_items", "{\"item\":\"madera\"}", out error)?.Kind.ToString(), "GiveItems");

        ActionRequest last = CompanionActions.Parse("give_items", "{\"item\":\"last_task\"}", out error);
        Check("dar: última tarea", last?.Item, CompanionActions.LastTaskItems);
        Check("dar: es tarea en cadena", CompanionActions.IsExclusiveTask(ActionKind.GiveItems).ToString(), "True");
        Check("dar: cantidad", CompanionActions.Parse("give_items", "{\"item\":\"madera\",\"amount\":3}", out error)?.Amount.ToString(), "3");
        Check("dar: sin cantidad es todo", CompanionActions.Parse("give_items", "{\"item\":\"madera\"}", out error)?.Amount.ToString(), "0");
        Check("dar: sin objeto", (CompanionActions.Parse("give_items", "{}", out error) == null).ToString(), "True");
        Check("dar: objeto vacío", (CompanionActions.Parse("give_items", "{\"item\":\"  \"}", out error) == null).ToString(), "True");
        Check("dar: cantidad cero", (CompanionActions.Parse("give_items", "{\"item\":\"madera\",\"amount\":0}", out error) == null).ToString(), "True");
        Check("dar: cantidad decimal", (CompanionActions.Parse("give_items", "{\"item\":\"madera\",\"amount\":1.5}", out error) == null).ToString(), "True");
    }

    private static void CheckGoals()
    {
        var plant = new PlantGoal(0f);
        Check("planta: lejos", plant.Next(true, 5f, 1f).ToString(), "Wait");
        Check("planta: lejos no pausa la IA", plant.InReach.ToString(), "False");
        Check("planta: al llegar apunta primero", plant.Next(true, 1.4f, 2f).ToString(), "Wait");
        Check("planta: al llegar pausa la IA", plant.InReach.ToString(), "True");
        Check("planta: golpea tras apuntar", plant.Next(true, 1.4f, 2f + PlantGoal.AimDelay).ToString(), "Swing");
        Check("planta: espera el siguiente golpe", plant.Next(true, 1.4f, 2.5f).ToString(), "Wait");
        Check("planta: golpea otra vez", plant.Next(true, 1.4f, 2f + PlantGoal.AimDelay + PlantGoal.SwingInterval).ToString(), "Swing");
        Check("planta: un retroceso no la detiene", plant.Next(true, 1.8f, 2f + PlantGoal.AimDelay + 2f * PlantGoal.SwingInterval).ToString(), "Swing");
        Check("planta: rota", plant.Next(false, 0f, 6f).ToString(), "Done");
        Check("planta: tiempo agotado", new PlantGoal(0f).Next(true, 1f, PlantGoal.TimeoutSeconds).ToString(), "TimedOut");
        Check("trabajo de piedra", CompanionActions.JobForItem("Stone").ToString(), CompanionActions.JobStone.ToString());
        Check("trabajo de cobre", CompanionActions.JobForItem("CopperOre").ToString(), CompanionActions.JobOre.ToString());
        Check("objeto desconocido", CompanionActions.JobForItem("Coins").ToString(), "0");

        var tree = new TreeGoal(0f);
        Check("árbol: en pie", tree.Update(true, 0, 1f).ToString(), "Working");
        Check("árbol: quedan troncos", tree.Update(false, 2, 2f).ToString(), "Working");
        Check("árbol: margen para recoger", tree.Update(false, 0, 3f).ToString(), "Working");
        Check("árbol: aparece otro trozo", tree.Update(false, 1, 4f).ToString(), "Working");
        Check("árbol: reinicia el margen", tree.Update(false, 0, 5f).ToString(), "Working");
        Check("árbol: todavía dentro del margen", tree.Update(false, 0, 5f + TreeGoal.SettleSeconds - 1f).ToString(), "Working");
        Check("árbol: terminado", tree.Update(false, 0, 5f + TreeGoal.SettleSeconds).ToString(), "Reached");
        Check("árbol: tiempo agotado", new TreeGoal(0f).Update(true, 3, TreeGoal.TimeoutSeconds).ToString(), "TimedOut");

        var goal = new GatherGoal("Stone", "piedra", 10, 3, 0f);
        Check("meta: lo previo no cuenta", goal.Update(3, true, 1f) + "/" + goal.Gained, "Working/0");
        Check("meta: suma lo nuevo", goal.Update(8, true, 2f) + "/" + goal.Gained, "Working/5");
        Check("meta: vaciar la mochila no resta", goal.Update(0, true, 3f) + "/" + goal.Gained, "Working/5");
        Check("meta: sigue sumando tras vaciar", goal.Update(4, true, 4f) + "/" + goal.Gained, "Working/9");
        Check("meta: conseguida", goal.Update(6, true, 5f) + "/" + goal.Gained, "Reached/11");

        var stuck = new GatherGoal("Stone", "piedra", 10, 0, 0f);
        Check("meta: espera sin atascarse", stuck.Update(0, false, GatherGoal.StuckSeconds - 1f).ToString(), "Working");
        Check("meta: trabajar reinicia la espera", stuck.Update(0, true, GatherGoal.StuckSeconds - 1f).ToString(), "Working");
        Check("meta: sigue dentro del margen", stuck.Update(0, false, GatherGoal.StuckSeconds + 1f).ToString(), "Working");
        Check("meta: atascada", stuck.Update(0, false, 2f * GatherGoal.StuckSeconds).ToString(), "Stuck");

        var partial = new GatherGoal("Stone", "piedra", 10, 0, 0f);
        Check("meta: consigue algo", partial.Update(1, true, 1f).ToString(), "Working");
        Check("meta: con algo espera poco", partial.Update(1, false, GatherGoal.StuckWithSomeSeconds).ToString(), "Working");
        Check("meta: con algo termina antes", partial.Update(1, false, 1f + GatherGoal.StuckWithSomeSeconds).ToString(), "Stuck");

        var slow = new GatherGoal("Stone", "piedra", 10, 0, 0f);
        Check("meta: tiempo agotado", slow.Update(1, true, GatherGoal.TimeoutSeconds).ToString(), "TimedOut");

        string error;
        ActionRequest gather = CompanionActions.Parse("gather", "{\"item\":\"Stone\",\"amount\":10}", out error);
        Check("gather: trabajo de piedra", gather?.Value.ToString(), "2");
        Check("gather: objeto", gather?.Item, "Stone");
        Check("gather: cantidad", gather?.Amount.ToString(), "10");
        Check("gather: es tarea", CompanionActions.IsExclusiveTask(ActionKind.Gather).ToString(), "True");
        Check("gather: sin cantidad", (CompanionActions.Parse("gather", "{\"item\":\"Stone\"}", out error) == null).ToString(), "True");
        Check("gather: cantidad cero", (CompanionActions.Parse("gather", "{\"item\":\"Stone\",\"amount\":0}", out error) == null).ToString(), "True");
        Check("gather: cantidad excesiva", (CompanionActions.Parse("gather", "{\"item\":\"Stone\",\"amount\":99999}", out error) == null).ToString(), "True");
        Check("gather: cantidad decimal", (CompanionActions.Parse("gather", "{\"item\":\"Stone\",\"amount\":1.5}", out error) == null).ToString(), "True");
        Check("gather: objeto inventado", (CompanionActions.Parse("gather", "{\"item\":\"Coins\",\"amount\":5}", out error) == null).ToString(), "True");

        Check("trabajo: zona por defecto", CompanionActions.Parse("set_job", "{\"job\":\"stone\"}", out error)?.KeepFollowing.ToString(), "False");
        Check("trabajo: seguir si se pide", CompanionActions.Parse("set_job", "{\"job\":\"stone\",\"keep_following\":true}", out error)?.KeepFollowing.ToString(), "True");
        Check("zona: piedra", CompanionActions.UsesWorkArea(2).ToString(), "True");
        Check("zona: cazar", CompanionActions.UsesWorkArea(7).ToString(), "True");
        Check("zona: cocinar no", CompanionActions.UsesWorkArea(17).ToString(), "False");
        Check("zona: sin trabajo no", CompanionActions.UsesWorkArea(0).ToString(), "False");
    }

    private static void CheckVoice()
    {
        Check("inicio: cuerpo", VoiceProtocol.StartBody(15), "{\"timeout\":15}");
        Check("inicio: minimo", VoiceProtocol.StartBody(1), "{\"timeout\":3}");
        Check("inicio: maximo", VoiceProtocol.StartBody(600), "{\"timeout\":60}");
        Check("sigue escuchando", VoiceProtocol.ShouldStop(10f, 15).ToString(), "False");
        Check("termina antes del plazo", VoiceProtocol.ShouldStop(14.6f, 15).ToString(), "True");
        Check("texto", VoiceProtocol.ParseText("{\"text\":\" Hola \"}"), "Hola");
        Check("texto vacio", VoiceProtocol.ParseText("{\"text\":\"\"}"), null);
        Check("sin texto", VoiceProtocol.ParseText("{}"), null);
        Check("respuesta rota", VoiceProtocol.ParseText("<html>"), null);
        Check("error: app cerrada", VoiceProtocol.DescribeError(0, null).Contains("app").ToString(), "True");
        Check("error: tiempo", VoiceProtocol.DescribeError(404, null).Contains("tiempo").ToString(), "True");
        Check("error: saldo", VoiceProtocol.DescribeError(402, "insufficient_credits").Contains("saldo").ToString(), "True");
    }

    private static void CheckPlanning()
    {
        string unknown;
        var names = new[] { "Saber", "Bjorn", "Elsa", "Ulf", "Astrid" };
        Check("sin who: quien habla", string.Join(",", Recipients.Resolve(null, names, out unknown)), "0");
        Check("all: máximo 4", string.Join(",", Recipients.Resolve("all", names, out unknown)), "0,1,2,3");
        Check("ALL en mayúsculas", Recipients.Resolve("ALL", names, out unknown).Count.ToString(), "4");
        Check("por nombres", string.Join(",", Recipients.Resolve(" bjorn , Elsa", names, out unknown)), "1,2");
        Check("nombre sin duplicar", string.Join(",", Recipients.Resolve("Elsa,Elsa", names, out unknown)), "2");
        Recipients.Resolve("Bjorn,Odín", names, out unknown);
        Check("nombre desconocido", unknown, "Odín");
        Check("nombres: máximo 4", Recipients.Resolve("Saber,Bjorn,Elsa,Ulf,Astrid", names, out unknown).Count.ToString(), "4");
        Check("nombres repetidos: todos", string.Join(",", Recipients.Resolve("Companion", new[] { "Companion", "Companion" }, out unknown)), "0,1");
        Check("sin compañeros", Recipients.Resolve("all", new string[0], out unknown).Count.ToString(), "0");

        Check("nombrado al inicio", Recipients.FindAddressed("Bjorn, ven aquí", names).ToString(), "1");
        Check("nombrado sin mayúscula", Recipients.FindAddressed("elsa, tala", names).ToString(), "2");
        Check("el primero nombrado", Recipients.FindAddressed("Ulf y Bjorn, vengan", names).ToString(), "3");
        Check("nombre con acento", Recipients.FindAddressed("Óðinn, ven", new[] { "x", "Óðinn" }).ToString(), "1");
        Check("no dentro de otra palabra", Recipients.FindAddressed("El saberlo no ayuda", names).ToString(), "-1");
        Check("nadie nombrado", Recipients.FindAddressed("Ven aquí", names).ToString(), "-1");

        var seq = new OrderSequence<string>();
        seq.Enqueue("b");
        seq.StepStarted(100f);
        Check("margen inicial", seq.Tick(100.5f, false).ToString(), "Wait");
        Check("sigue ocupado", seq.Tick(110f, true).ToString(), "Wait");
        Check("pasa a la siguiente", seq.Tick(110f, false).ToString(), "StartNext");
        Check("tiempo agotado", seq.Tick(100f + OrderSequence<string>.StepTimeout, true).ToString(), "TimedOut");
        Check("saca la siguiente", seq.TakeNext(), "b");
        Check("terminada", seq.Tick(110f, false).ToString(), "Finished");

        var full = new OrderSequence<string>();
        for (int i = 0; i < OrderSequence<string>.MaxQueued; i++) full.Enqueue("x");
        Check("cola llena", full.Enqueue("y").ToString(), "False");
        full.Clear();
        Check("vaciar cola", full.Pending.ToString(), "0");

        string error;
        Check("hogar", CompanionActions.Parse("set_home", "{}", out error)?.Kind.ToString(), "SetHome");
        Check("opción: valor", CompanionActions.Parse("set_option", "{\"option\":\"pickup\",\"enabled\":false}", out error)?.Value.ToString(), CompanionActions.OptionPickup.ToString());
        Check("opción: estado", CompanionActions.Parse("set_option", "{\"option\":\"pickup\",\"enabled\":false}", out error)?.Enabled.ToString(), "False");
        Check("opción sin estado", (CompanionActions.Parse("set_option", "{\"option\":\"pickup\"}", out error) == null).ToString(), "True");
        Check("opción inventada", (CompanionActions.Parse("set_option", "{\"option\":\"despawn\",\"enabled\":true}", out error) == null).ToString(), "True");
        Check("lee who", CompanionActions.Parse("come_here", "{\"who\":\"all\"}", out error)?.Who, "all");
        Check("who no textual", CompanionActions.Parse("come_here", "{\"who\":3}", out error)?.Who, null);
        Check("hogar no es tarea", CompanionActions.IsExclusiveTask(ActionKind.SetHome).ToString(), "False");
        Check("venir vacía la cola", CompanionActions.ClearsQueue(ActionKind.ComeHere).ToString(), "True");
        Check("atacar no vacía la cola", CompanionActions.ClearsQueue(ActionKind.Attack).ToString(), "False");
        foreach (JToken tool in CompanionActions.ToolDefinitions())
        {
            Check("who en " + tool["function"]["name"], (tool["function"]["parameters"]["properties"]["who"] != null).ToString(), "True");
        }
    }

    /// <summary>Comprueba cómo trata al jugador un personaje real, con su ficha y su memoria tal como están guardadas.</summary>
    private static void LiveSheet(string sheetPath, string memoryPath)
    {
        var sheet = Newtonsoft.Json.JsonConvert.DeserializeObject<CharacterProfile>(File.ReadAllText(sheetPath, Encoding.UTF8));
        var memory = Newtonsoft.Json.JsonConvert.DeserializeObject<CompanionMemory>(File.ReadAllText(memoryPath, Encoding.UTF8));
        string prompt = PromptBuilder.Build(sheet, "", "español",
            "Tu nombre: " + sheet.Name + "\nJugador: Roman\nLo que mira el jugador: nada en particular\nLo que haces ahora: Siguiéndote\nBioma: Meadows\nMomento: día\nEnemigos cerca: ninguno",
            memory.Summary, true);
        foreach (string question in new[] { "Hola, ¿cómo estás hoy?", "Recoge 10 de piedra.", "Gracias por acompañarme." })
        {
            var messages = new List<ChatMessage> { new ChatMessage("system", prompt) };
            messages.AddRange(memory.ToMessages(12));
            messages.Add(new ChatMessage("user", question));
            var sb = new StringBuilder();
            LlmResponse response = Call(messages, null, sb);
            Console.WriteLine("Tú: " + question + "\n" + sheet.Name + ": " + response?.Text + "\n");
        }
    }

    /// <summary>Lo que dice al actuar por su cuenta, con un historial de frases largas que tiende a imitar, y si conoce el equipo del jugador.</summary>
    private static void LiveShort()
    {
        const string sheet = "Eres Saber, una guerrera noble, orgullosa y algo dramática, que habla con grandilocuencia.";
        const string state = "Tu nombre: Saber\nJugador: Leif, a 5 m de ti\nLo que mira el jugador: nada en particular\nLo que lleva equipado el jugador: Maza de bronce, Escudo de madera, Casco de cuero, Peto de bronce (nivel 2) (coméntalo solo si viene a cuento)\nTu salud: 60/60\nTu aguante: 90 %\nLo que haces ahora: Siguiéndote\nBioma: Meadows\nMomento: día\nEnemigos cerca: ninguno\nTu inventario: 1 Hacha de piedra\nOtros compañeros cerca: ninguno";
        string prompt = PromptBuilder.Build(new CharacterProfile { Instructions = sheet }, "", "español", state, null, false, true);
        var history = new List<ChatMessage>
        {
            new ChatMessage("user", Initiative.MemoryLine),
            new ChatMessage("assistant", "Iré por madera porque mientras tú haces los cimientos, yo talaré los árboles para que la casa brille con todo su esplendor, Leif."),
            new ChatMessage("user", Initiative.MemoryLine),
            new ChatMessage("assistant", "Mientras tú levantas esa casa, yo junto piedra para los cimientos; y no te preocupes si un grisucio se acerca, que aquí está mi escudo esperándolo con gusto.")
        };
        var facts = new InitiativeFacts { BuiltChests = 3, GatheredInPack = true, Animals = true, Trees = true, Ripe = true, Rocks = true };
        const string around = "A menos de 40 m: 12 árboles en pie; 4 rocas o vetas; 3 cofres construidos por jugadores, el más cercano a 7 m al norte; para recoger: 6 Frambuesas; 1 Ciervo (animal que huye).";
        var random = new Random();
        int total = 0, count = 0;
        for (int turn = 0; turn < 6; turn++)
        {
            List<InitiativeIdea> picked = Initiative.Pick(Initiative.Ideas(facts, key => false), 3, random);
            var messages = new List<ChatMessage> { new ChatMessage("system", prompt) };
            messages.AddRange(history);
            messages.Add(new ChatMessage("user", Initiative.Message(around, null, true, picked.ConvertAll(idea => idea.Text))));
            var sb = new StringBuilder();
            LlmResponse first = Call(messages, Initiative.ToolsFor(picked), sb);
            if (first == null) continue;
            string text = first.Text;
            string calls = "";
            if (first.ToolCalls.Count > 0)
            {
                messages.Add(new ChatMessage("assistant", first.Text, first.RawToolCalls));
                foreach (ToolCall call in first.ToolCalls)
                {
                    calls += call.Name + " " + call.Arguments + "; ";
                    messages.Add(new ChatMessage("tool", "Orden aceptada.", null, call.Id));
                }
                messages.Add(new ChatMessage("system", Initiative.AfterActing));
                text = Call(messages, null, sb)?.Text;
            }
            string spoken = LlmProtocol.StripNarration(Initiative.StripSilence(text));
            int words = spoken.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (first.ToolCalls.Count > 0) { total += words; count++; }
            Console.WriteLine("Turno " + (turn + 1) + " [" + words + " palabras] " + calls + "-> " + spoken);
        }
        if (count > 0) Console.WriteLine("Media al actuar: " + (total / count) + " palabras");

        foreach (string question in new[] { "¿qué llevo puesto?", "¿crees que voy bien protegido?", "Hola, ¿cómo estás?" })
        {
            var sb = new StringBuilder();
            LlmResponse response = Call(new List<ChatMessage> { new ChatMessage("system", prompt), new ChatMessage("user", question) }, null, sb);
            Console.WriteLine("Tú: " + question + " -> " + response?.Text);
        }
    }

    /// <summary>Qué ocupa cada parte de una petición, sin usar la red.</summary>
    private static void PrintRequestSizes()
    {
        JArray tools = CompanionActions.ToolDefinitions();
        string toolsJson = tools.ToString(Newtonsoft.Json.Formatting.None);
        string fixedPrompt = PromptBuilder.Build(new CharacterProfile { Instructions = "Eres Saber, una vikinga directa y bromista." }, "", "español", "", null, true);
        Console.WriteLine("Herramientas: " + tools.Count + ", " + toolsJson.Length + " caracteres");
        Console.WriteLine("Instrucciones del mod con ficha mínima y sin situación: " + fixedPrompt.Length + " caracteres");
        var sizes = new List<KeyValuePair<string, int>>();
        foreach (JToken tool in tools) sizes.Add(new KeyValuePair<string, int>(tool.SelectToken("function.name")?.ToString(), tool.ToString(Newtonsoft.Json.Formatting.None).Length));
        sizes.Sort((a, b) => b.Value - a.Value);
        foreach (KeyValuePair<string, int> size in sizes) Console.WriteLine("  " + size.Key + ": " + size.Value);
    }

    private sealed class CompareCase
    {
        public string Title;
        public List<ChatMessage> Messages;
        public JArray Tools;
        public Func<List<ToolCall>, string, string> Judge; // Devuelve el motivo del fallo, o null si acierta.
        public bool ExpectsAction;
        public bool Multi;
        public bool FollowUp;
    }

    private static string ArgOf(ToolCall call, string name)
    {
        try { return JObject.Parse(string.IsNullOrEmpty(call.Arguments) ? "{}" : call.Arguments)[name]?.ToString(); }
        catch (Newtonsoft.Json.JsonException) { return null; }
    }

    private static List<CompareCase> CompareCases()
    {
        const string sheet = "Eres Saber, una vikinga directa y bromista.";
        const string reminder = "Recordatorio: las órdenes de mensajes anteriores ya se atendieron; no las repitas. Atiende solo el siguiente mensaje: si es una orden, usa la herramienta de esa orden, porque tus frases no hacen nada en el juego. Si no es una orden, responde sin herramientas.";
        var profile = new CharacterProfile { Instructions = sheet };
        string calm = PromptBuilder.Build(profile, "", "español",
            "Tu nombre: Saber\nJugador: Leif, a 5 m de ti\nLo que mira el jugador: nada en particular\nTu salud: 60/60\nLo que haces ahora: Siguiéndote\nBioma: Meadows\nMomento: día\nEnemigos cerca: ninguno\n"
            + "Tus herramientas (hacha: sí, pico: no).\nTu inventario: 1 Hacha de piedra, 12 Madera\nOtros compañeros cerca: Bjorn, Elsa\n"
            + "Tu última tarea, ya terminada: conseguir 10 de madera. Ganaste 11 Madera. Si el jugador pregunta por ella, cuéntale cómo fue o dale lo que lleves, pero no la repitas salvo que te lo pida",
            null, true);
        string danger = PromptBuilder.Build(profile, "", "español",
            "Tu nombre: Saber\nJugador: Leif\nLo que mira el jugador: Jabalí\nTu salud: 40/60\nLo que haces ahora: Peleando\nBioma: Meadows\nMomento: día\nEnemigos cerca: 2 Greydwarf, 1 Jabalí");
        string free = PromptBuilder.Build(profile, "", "español",
            "Tu nombre: Saber\nJugador: Leif, a 5 m de ti\nMomento: día 12, por la mañana\nSalud del jugador: 100/100, aguante 90 %\nLo que mira el jugador: nada en particular\nTu salud: 60/60\nLo que haces ahora: Siguiéndote\nBioma: Meadows\nEnemigos cerca: ninguno\nTus herramientas (hacha: sí, pico: no).\nTu inventario: 1 Hacha de piedra, 3 Frambuesas\nOtros compañeros cerca: ninguno",
            null, true, true);
        string hurt = free.Replace("Salud del jugador: 100/100, aguante 90 %", "Salud del jugador: 20/100 (malherido), aguante 40 %");
        JArray all = CompanionActions.ToolDefinitions();

        var cases = new List<CompareCase>();
        void Order(string line, Func<List<ToolCall>, string, string> judge, bool expectsAction = true, bool multi = false, bool followUp = false, string system = null)
        {
            cases.Add(new CompareCase
            {
                Title = line,
                Tools = all,
                Judge = judge,
                ExpectsAction = expectsAction,
                Multi = multi,
                FollowUp = followUp,
                Messages = new List<ChatMessage>
                {
                    new ChatMessage("system", system ?? calm),
                    new ChatMessage("user", "Sígueme."),
                    new ChatMessage("assistant", "Voy detrás de ti, Leif."),
                    new ChatMessage("system", reminder),
                    new ChatMessage("user", line)
                }
            });
        }
        Func<List<ToolCall>, string, string> Wants(string tool, Func<ToolCall, bool> ok = null)
        {
            return (calls, text) =>
            {
                ToolCall found = calls.Find(c => c.Name == tool);
                if (found == null) return "esperaba " + tool;
                return ok == null || ok(found) ? null : "argumentos incorrectos en " + tool;
            };
        }
        Func<List<ToolCall>, string, string> OnlyTalk = (calls, text) =>
            calls.Count > 0 ? "no debía actuar: " + calls[0].Name : string.IsNullOrWhiteSpace(text) ? "no respondió nada" : null;

        Order("Recoge 10 de piedra.", Wants("gather", c => ArgOf(c, "item") == "Stone" && ArgOf(c, "amount") == "10"));
        Order("Ven aquí.", (calls, text) => calls.Exists(c => c.Name == "come_here" || (c.Name == "set_follow" && ArgOf(c, "follow")?.ToLowerInvariant() == "true")) ? null : "esperaba come_here");
        Order("Protege esta zona.", Wants("guard_area"));
        Order("¡Ataca a ese jabalí!", Wants("attack", c => ArgOf(c, "target") == "looked_at"), system: danger);
        Order("agarra flechas para el arco, y despues consigue 15 de madera", (calls, text) =>
        {
            int take = calls.FindIndex(c => c.Name == "take_from_chest" && (ArgOf(c, "item") ?? "").IndexOf("flech", StringComparison.OrdinalIgnoreCase) >= 0);
            int gather = calls.FindIndex(c => c.Name == "gather" && ArgOf(c, "item") == "Wood" && ArgOf(c, "amount") == "15");
            return take < 0 || gather < 0 ? "faltan órdenes" : take > gather ? "orden invertido" : null;
        }, multi: true);
        Order("busca comida en los cofres", Wants("take_from_chest", c => ItemNames.IsFoodWord(ArgOf(c, "item") ?? "") && ArgOf(c, "amount") == null), followUp: true);
        Order("coge todas las frambuesas del cofre", Wants("take_from_chest", c => int.TryParse(ArgOf(c, "amount"), out int n) && n >= 9999));
        Order("ve al cofre que encontraste", Wants("go_to", c => ArgOf(c, "place") == "found_chest"));
        // La escena dice que el jugador no mira nada: pedirle que señale también es correcto; ir a otro sitio no.
        Order("Ve allí.", (calls, text) => calls.Exists(c => c.Name == "go_to" && ArgOf(c, "place") != "looked_at") ? "fue a otro sitio" : calls.Count == 0 && string.IsNullOrWhiteSpace(text) ? "no respondió nada" : null, expectsAction: false);
        Order("Cocina 3 carnes de jabalí.", Wants("cook_food", c => ArgOf(c, "amount") == "3"), followUp: true);
        Order("¿Qué hay en los cofres?", Wants("check_chests"));
        Order("¿Quién de ustedes pesca mejor?", Wants("check_skills", c => !string.IsNullOrEmpty(ArgOf(c, "who"))));
        // recall_all también los trae, aunque alcance a los que estén lejos.
        Order("Vengan todos aquí.", (calls, text) => calls.Exists(c => c.Name == "recall_all" || (c.Name == "come_here" && ArgOf(c, "who") == "all")) ? null : "esperaba come_here con all");
        Order("Ve hasta el campamento de los trolls.", (calls, text) => calls.Exists(c => c.Name == "go_to") ? "caminó a otro sitio" : null, expectsAction: false);
        Order("Hola, ¿cómo estás hoy?", OnlyTalk, expectsAction: false);
        Order("y la madera?", (calls, text) => calls.Exists(c => c.Name == "gather" || c.Name == "set_job") ? "repitió el encargo" : null, expectsAction: false);
        Order("¿Qué opinas de Odín?", OnlyTalk, expectsAction: false);

        string hungry = Initiative.EventMessage(new Reaction { Kind = ReactionKind.CompanionNeed, CanResolve = true, Description = "Tienes hambre y no llevas nada de comer. Si puede haber en los cofres, usa take_from_chest con item comida." });
        cases.Add(new CompareCase { Title = "(suceso) tiene hambre", Tools = Initiative.Tools(), ExpectsAction = true, Messages = new List<ChatMessage> { new ChatMessage("system", free), new ChatMessage("user", hungry) }, Judge = Wants("take_from_chest", c => ItemNames.IsFoodWord(ArgOf(c, "item") ?? "")) });

        const string around = "A menos de 40 m: 12 árboles en pie; 3 cofres construidos por jugadores, el más cercano a 7 m al norte; para recoger: 4 Diente de león; 1 Ciervo (animal que huye).";
        var work = new InitiativeFacts { BuiltChests = 3, Trees = true, Ripe = true, Animals = true, WoodStored = 5, FoodStored = 4, CookingSpot = true, CookableDish = "Carne de jabalí cocinada" };
        List<InitiativeIdea> picked = Initiative.Pick(Initiative.Ideas(work, key => false), 3, new Random(7));
        cases.Add(new CompareCase { Title = "(iniciativa) día tranquilo", Tools = Initiative.ToolsFor(picked), ExpectsAction = true, Messages = new List<ChatMessage> { new ChatMessage("system", free), new ChatMessage("user", Initiative.Message(around, null, true, picked.ConvertAll(idea => idea.Text), null, Initiative.Context(work))) }, Judge = (calls, text) => calls.Count == 0 ? "no hizo nada" : null });

        var close = new InitiativeFacts { BuiltChests = 3, Trees = true, Ripe = true, Animals = true, WoodStored = 5, FoodStored = 4, PlayerHurt = true };
        List<InitiativeIdea> nearby = Initiative.Pick(Initiative.Ideas(close, key => false), 3, new Random(7));
        cases.Add(new CompareCase { Title = "(iniciativa) jugador malherido", Tools = Initiative.ToolsFor(nearby, close.MustStayClose), ExpectsAction = false, Messages = new List<ChatMessage> { new ChatMessage("system", hurt), new ChatMessage("user", Initiative.Message(around, null, true, nearby.ConvertAll(idea => idea.Text), null, Initiative.Context(close))) }, Judge = (calls, text) => calls.Exists(c => (ArgOf(c, "who") ?? "").IndexOf("Leif", StringComparison.OrdinalIgnoreCase) >= 0) ? "intentó curar al jugador" : null });
        return cases;
    }

    /// <summary>La misma tanda con puntuación para cada modelo: obediencia, tiempo, tokens y joules. Se para si gasta de más.</summary>
    private static void LiveCompare(int repeats, int limit)
    {
        _modelTally.Clear();
        const long maxSpend = 45;
        long? before = FetchJoules();
        string model = null;
        int passed = 0, total = 0, faked = 0, wrong = 0, errors = 0, empty = 0, promptTokens = 0, completionTokens = 0, followUps = 0, followUpText = 0, calls = 0;
        var seconds = new List<double>();
        var report = new StringBuilder();
        List<CompareCase> cases = CompareCases();
        if (limit > 0 && limit < cases.Count) cases = cases.GetRange(0, limit);
        var perCase = new Dictionary<string, int>();
        var ran = new Dictionary<string, int>();

        for (int pass = 0; pass < repeats; pass++)
        {
            foreach (CompareCase test in cases)
            {
                long? now = FetchJoules();
                if (before.HasValue && now.HasValue && before.Value - now.Value > maxSpend)
                {
                    Console.WriteLine("PARADA: se llegó al tope de " + maxSpend + " J");
                    pass = repeats;
                    break;
                }

                var messages = new List<ChatMessage>(test.Messages);
                var made = new List<ToolCall>();
                string text = null, failure = null;
                for (int round = 1; round <= (test.Multi ? 3 : 1); round++)
                {
                    LlmResponse response = CompareCall(messages, test.Tools, seconds, ref model, ref promptTokens, ref completionTokens, ref errors, ref calls);
                    if (response == null) { if (round == 1) { empty++; failure = "sin respuesta"; } break; }
                    text = response.Text;
                    made.AddRange(response.ToolCalls);
                    if (response.ToolCalls.Count == 0 || !test.Multi) break;
                    messages.Add(new ChatMessage("assistant", response.Text, response.RawToolCalls));
                    foreach (ToolCall call in response.ToolCalls) messages.Add(new ChatMessage("tool", "Orden aceptada.", null, call.Id));
                    messages.Add(new ChatMessage("system", PromptBuilder.AfterOrders));
                }

                foreach (ToolCall call in made)
                {
                    if (CompanionActions.Parse(call.Name, call.Arguments, out string error) == null) failure = failure ?? "orden rechazada: " + call.Name + " (" + error + ")";
                }
                failure = failure ?? test.Judge(made, text);
                if (failure != null && test.ExpectsAction && made.Count == 0 && !string.IsNullOrWhiteSpace(text)) { faked++; failure += " (solo habló)"; }
                else if (failure != null) wrong++;

                if (test.FollowUp && made.Count > 0)
                {
                    followUps++;
                    var after = new List<ChatMessage>(messages) { new ChatMessage("assistant", text, new JArray(made.ConvertAll(c => (JToken)new JObject { ["id"] = c.Id, ["type"] = "function", ["function"] = new JObject { ["name"] = c.Name, ["arguments"] = c.Arguments } }))) };
                    foreach (ToolCall call in made) after.Add(new ChatMessage("tool", "Orden aceptada.", null, call.Id));
                    after.Add(new ChatMessage("system", PromptBuilder.AfterOrders));
                    LlmResponse said = CompareCall(after, null, seconds, ref model, ref promptTokens, ref completionTokens, ref errors, ref calls);
                    if (!string.IsNullOrWhiteSpace(said?.Text)) { followUpText++; text = said.Text; }
                }

                total++;
                ran[test.Title] = (ran.TryGetValue(test.Title, out int runs) ? runs : 0) + 1;
                if (failure == null) { passed++; perCase[test.Title] = (perCase.TryGetValue(test.Title, out int soFar) ? soFar : 0) + 1; }
                string done = string.Join("; ", made.ConvertAll(c => c.Name + " " + c.Arguments));
                report.AppendLine((failure == null ? "BIEN  " : "FALLO ") + test.Title + (failure != null ? " -> " + failure : ""));
                report.AppendLine("      Acción: " + (done.Length > 0 ? done : "(ninguna)"));
                report.AppendLine("      Dice: " + (text ?? "(nada)"));
            }
        }

        long? after2 = FetchJoules();
        seconds.Sort();
        string main = model;
        foreach (KeyValuePair<string, int> entry in _modelTally)
        {
            if (main == null || !_modelTally.ContainsKey(main) || entry.Value > _modelTally[main]) main = entry.Key;
        }
        string safeModel = System.Text.RegularExpressions.Regex.Replace(main ?? "desconocido", @"[^\w.-]", "_");
        var summary = new StringBuilder();
        var tally = new List<string>();
        foreach (KeyValuePair<string, int> entry in _modelTally) tally.Add(entry.Key + " x" + entry.Value);
        summary.AppendLine("Modelos que respondieron según Player2: " + string.Join(", ", tally));
        summary.AppendLine("Aciertos: " + passed + "/" + total + "; solo habló sin actuar: " + faked + "; acción equivocada: " + wrong + "; respuestas vacías: " + empty + "; errores HTTP: " + errors);
        summary.AppendLine("Texto tras actuar: " + followUpText + "/" + followUps);
        if (seconds.Count > 0) summary.AppendLine("Segundos: mediana " + seconds[seconds.Count / 2].ToString("0.0") + ", máximo " + seconds[seconds.Count - 1].ToString("0.0") + " en " + calls + " llamadas");
        summary.AppendLine("Tokens: " + promptTokens + " de entrada, " + completionTokens + " de salida");
        summary.AppendLine("Joules: " + before + " -> " + after2);
        foreach (CompareCase test in cases)
        {
            int ok = perCase.TryGetValue(test.Title, out int count) ? count : 0;
            int times = ran.TryGetValue(test.Title, out int done) ? done : 0;
            if (ok < times) summary.AppendLine("  Falla " + (times - ok) + "/" + times + ": " + test.Title);
        }
        string file = Path.Combine(Path.GetTempPath(), "ocai_compare_" + safeModel + ".txt");
        File.WriteAllText(file, summary + "\n" + report, new UTF8Encoding(false));
        Console.Write(summary);
        Console.WriteLine("Detalle en " + file);
    }

    private static readonly Dictionary<string, int> _modelTally = new Dictionary<string, int>();

    private static long? FetchJoules()
    {
        using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
        {
            http.DefaultRequestHeaders.Add("player2-game-key", "OfflineCompanionsAI");
            try { return LlmProtocol.ParseJoules(http.GetStringAsync("http://localhost:4315/v1/joules").Result); }
            catch (AggregateException) { return null; }
        }
    }

    private static LlmResponse CompareCall(List<ChatMessage> messages, JArray tools, List<double> seconds, ref string model, ref int promptTokens, ref int completionTokens, ref int errors, ref int calls)
    {
        using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
        {
            http.DefaultRequestHeaders.Add("player2-game-key", "OfflineCompanionsAI");
            DateTime started = DateTime.UtcNow;
            calls++;
            try
            {
                string body = LlmProtocol.BuildRequest(messages, null, 0, 0.8f, tools);
                HttpResponseMessage res = http.PostAsync("http://localhost:4315/v1/chat/completions", new StringContent(body, Encoding.UTF8, "application/json")).Result;
                string text = res.Content.ReadAsStringAsync().Result;
                seconds.Add((DateTime.UtcNow - started).TotalSeconds);
                if (!res.IsSuccessStatusCode)
                {
                    errors++;
                    Console.WriteLine("HTTP " + (int)res.StatusCode + ": " + text);
                    return null;
                }
                JObject root = JObject.Parse(text);
                model = root["model"]?.ToString() ?? model;
                if (model != null) _modelTally[model] = (_modelTally.TryGetValue(model, out int seen) ? seen : 0) + 1;
                promptTokens += root.SelectToken("usage.prompt_tokens")?.Value<int>() ?? 0;
                completionTokens += root.SelectToken("usage.completion_tokens")?.Value<int>() ?? 0;
                return LlmProtocol.ParseResponse(text);
            }
            catch (AggregateException e)
            {
                errors++;
                Console.WriteLine("Error de red: " + e.InnerException?.Message);
                return null;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                errors++;
                return null;
            }
        }
    }

    /// <summary>Lo que hace por su cuenta según la situación: de noche, con incursión, con el jugador herido o construyendo.</summary>
    private static void LiveContext()
    {
        const string sheet = "Eres Saber, una vikinga directa y bromista.";
        const string around = "A menos de 40 m: 12 árboles en pie; 3 cofres construidos por jugadores, el más cercano a 7 m al norte; para recoger: 4 Diente de león; 1 Ciervo (animal que huye).";
        InitiativeFacts Base() => new InitiativeFacts { BuiltChests = 3, Trees = true, Ripe = true, Animals = true, WoodStored = 5, FoodStored = 4, CookingSpot = true, CookableDish = "Carne de jabalí cocinada" };

        var scenes = new[]
        {
            new { Title = "Incursión", Line = "Momento: día 12, por la tarde\nIncursión en marcha: El bosque se mueve...\nSalud del jugador: 80/100, aguante 90 %", Facts = Fix(Base(), f => f.Raid = true) },
            new { Title = "Jugador malherido", Line = "Momento: día 12, por la tarde\nSalud del jugador: 20/100 (malherido), aguante 40 %", Facts = Fix(Base(), f => f.PlayerHurt = true) },
            new { Title = "Noche fuera de la base", Line = "Momento: día 12, de noche\nSalud del jugador: 90/100, aguante 90 %", Facts = Fix(Base(), f => f.Night = true) },
            new { Title = "Construyendo de día", Line = "Momento: día 12, por la mañana\nSalud del jugador: 100/100, aguante 90 %\nEl jugador está construyendo.", Facts = Fix(Base(), f => { f.AtBase = true; f.PlayerBuilding = true; }) }
        };
        var random = new Random();
        foreach (var scene in scenes)
        {
            for (int turn = 0; turn < 2; turn++)
            {
                string state = "Tu nombre: Saber\nJugador: Leif, a 5 m de ti\n" + scene.Line + "\nLo que mira el jugador: nada en particular\nTu salud: 60/60\nLo que haces ahora: Siguiéndote\nBioma: Meadows\nEnemigos cerca: ninguno\nTus herramientas (hacha: sí, pico: no).\nTu inventario: 1 Hacha de piedra, 3 Frambuesas\nOtros compañeros cerca: ninguno";
                string prompt = PromptBuilder.Build(new CharacterProfile { Instructions = sheet }, "", "español", state, null, true, true);
                List<InitiativeIdea> picked = Initiative.Pick(Initiative.Ideas(scene.Facts, key => false), 3, random);
                string message = Initiative.Message(around, null, true, picked.ConvertAll(idea => idea.Text), null, Initiative.Context(scene.Facts));
                var sb = new StringBuilder();
                LlmResponse response = Call(new List<ChatMessage> { new ChatMessage("system", prompt), new ChatMessage("user", message) }, Initiative.ToolsFor(picked, scene.Facts.MustStayClose), sb);
                string calls = "";
                foreach (ToolCall call in response?.ToolCalls ?? new List<ToolCall>()) calls += call.Name + " " + call.Arguments + "; ";
                Console.WriteLine(scene.Title + ": ideas [" + string.Join("; ", picked.ConvertAll(idea => idea.Key)) + "] -> " + (calls.Length > 0 ? calls : "(sin acción)") + " | Dice: " + Initiative.StripSilence(response?.Text));
            }
        }

        InitiativeFacts Fix(InitiativeFacts facts, Action<InitiativeFacts> change)
        {
            change(facts);
            return facts;
        }
    }

    /// <summary>Pedir comida en general, una en concreto o toda: el modelo debe decirlo con la cantidad correcta.</summary>
    private static void LiveFood()
    {
        string state = "Tu nombre: Saber\nJugador: Leif, a 5 m de ti\nLo que mira el jugador: nada en particular\nTu salud: 60/60\nLo que haces ahora: Siguiéndote\nBioma: Meadows\nMomento: día\nEnemigos cerca: ninguno\nTus herramientas (hacha: sí, pico: no).\nTu inventario: 1 Hacha de piedra\nOtros compañeros cerca: ninguno";
        string orders = PromptBuilder.Build(new CharacterProfile { Instructions = "Eres Saber, una vikinga directa y bromista." }, "", "español", state);
        string reminder = "Recordatorio: las órdenes de mensajes anteriores ya se atendieron; no las repitas. Atiende solo el siguiente mensaje: si es una orden, usa la herramienta de esa orden, porque tus frases no hacen nada en el juego. Si no es una orden, responde sin herramientas.";
        foreach (string order in new[] { "busca comida en los cofres", "agarra algo de comer", "coge frambuesas del cofre", "coge 5 frambuesas", "coge todas las frambuesas del cofre", "coge madera del cofre" })
        {
            var sb = new StringBuilder();
            LlmResponse response = Call(new List<ChatMessage> { new ChatMessage("system", orders), new ChatMessage("system", reminder), new ChatMessage("user", order) }, CompanionActions.ToolDefinitions(), sb);
            string calls = "";
            foreach (ToolCall call in response?.ToolCalls ?? new List<ToolCall>()) calls += call.Name + " " + call.Arguments + "; ";
            Console.WriteLine("Tú: " + order + " -> " + (calls.Length > 0 ? calls : "(sin acción) " + response?.Text));
        }

        string hungry = Initiative.EventMessage(new Reaction { Kind = ReactionKind.CompanionNeed, CanResolve = true, Description = "Tienes hambre y no llevas nada de comer. Si puede haber en los cofres, usa take_from_chest con item comida." });
        var talk = new StringBuilder();
        LlmResponse reply = Call(new List<ChatMessage> { new ChatMessage("system", PromptBuilder.Build(new CharacterProfile { Instructions = "Eres Saber, una vikinga directa y bromista." }, "", "español", state, null, false, true)), new ChatMessage("user", hungry) }, Initiative.Tools(), talk);
        string done = "";
        foreach (ToolCall call in reply?.ToolCalls ?? new List<ToolCall>()) done += call.Name + " " + call.Arguments + "; ";
        Console.WriteLine("Hambre -> " + (done.Length > 0 ? done : "(sin acción)") + " | Dice: " + reply?.Text);
    }

    /// <summary>Casos de la última prueba: flechas, cofres propios frente a encontrados, y variedad de lo que hace por su cuenta.</summary>
    private static void LiveGear()
    {
        const string sheet = "Eres Saber, una vikinga directa y bromista.";
        string state = "Tu nombre: Saber\nJugador: Leif, a 5 m de ti\nLo que mira el jugador: nada en particular\nTu salud: 60/60\nLo que haces ahora: Siguiéndote\nBioma: Meadows\nMomento: día\nEnemigos cerca: ninguno\n"
            + "Tus herramientas (hacha: sí, pico: no).\nTu arma a distancia: Arco tosco, sin flechas; arma cuerpo a cuerpo: sí. Si te faltan flechas, búscalas con take_from_chest; sin ellas peleas cuerpo a cuerpo.\nTu inventario: 1 Arco tosco, 1 Hacha de piedra, 8 Piedra\nOtros compañeros cerca: ninguno";
        string orders = PromptBuilder.Build(new CharacterProfile { Instructions = sheet }, "", "español", state);
        string reminder = "Recordatorio: las órdenes de mensajes anteriores ya se atendieron; no las repitas. Atiende solo el siguiente mensaje: si es una orden, usa la herramienta de esa orden, porque tus frases no hacen nada en el juego. Si no es una orden, responde sin herramientas.";
        foreach (string order in new[] { "agarra flechas para el arco, y despues consigue 15 de madera", "busca flechas", "ve al cofre que encontraste", "ve al cofre" })
        {
            var sb = new StringBuilder();
            LlmResponse response = Call(new List<ChatMessage> { new ChatMessage("system", orders), new ChatMessage("system", reminder), new ChatMessage("user", order) }, CompanionActions.ToolDefinitions(), sb);
            string calls = "";
            foreach (ToolCall call in response?.ToolCalls ?? new List<ToolCall>()) calls += call.Name + " " + call.Arguments + "; ";
            Console.WriteLine("Tú: " + order + " -> " + (calls.Length > 0 ? calls : "(sin acción) " + response?.Text));
        }

        string free = PromptBuilder.Build(new CharacterProfile { Instructions = sheet }, "", "español", state.Replace("sin flechas", "20 flechas"), null, true, true);
        const string around = "A menos de 40 m: 12 árboles en pie; 3 cofres construidos por jugadores, el más cercano a 7 m al norte; 2 camas; para recoger: 4 Diente de león; objetos sueltos: 1 Arco tosco; 1 Ciervo (animal que huye).";
        var facts = new InitiativeFacts { LooseItems = true, FiresLow = true, CanGetHammer = false, BuiltChests = 3, GatheredInPack = true, Animals = true, Trees = true, Ripe = true };
        var history = new InitiativeHistory();
        var random = new Random();
        for (int turn = 0; turn < 6; turn++)
        {
            float now = turn * 75f;
            List<InitiativeIdea> picked = Initiative.Pick(Initiative.Ideas(facts, key => history.WasRecent(key, now)), 3, random);
            string message = Initiative.Message(around, null, true, picked.ConvertAll(idea => idea.Text), history.Recent(now));
            var sb = new StringBuilder();
            LlmResponse response = Call(new List<ChatMessage> { new ChatMessage("system", free), new ChatMessage("user", message) }, Initiative.ToolsFor(picked), sb);
            string calls = "";
            foreach (ToolCall call in response?.ToolCalls ?? new List<ToolCall>())
            {
                ActionRequest action = CompanionActions.Parse(call.Name, call.Arguments, out string error);
                string refusal = action == null ? error : Initiative.Refusal(call.Name, action);
                string key = action != null ? Initiative.VarietyKey(call.Name, action) : null;
                if (refusal == null && key != null && history.WasRecent(key, now)) refusal = InitiativeHistory.RepeatRefusal;
                if (refusal == null && key != null) history.Add(key, action.Description, now);
                calls += call.Name + " " + call.Arguments + (refusal != null ? " RECHAZADA" : "") + "; ";
            }
            Console.WriteLine("Turno " + (turn + 1) + ": " + (calls.Length > 0 ? calls : "(sin acción)") + " | Dice: " + Initiative.StripSilence(response?.Text));
        }
    }

    /// <summary>Caso real: preguntar por un encargo ya terminado no debe hacer que lo repita.</summary>
    private static void LiveFinishedTask()
    {
        string prompt = PromptBuilder.Build(new CharacterProfile { Instructions = "Eres Saber, una vikinga directa y bromista." }, "", "español",
            "Tu nombre: Saber\nJugador: Leif\nLo que mira el jugador: nada en particular\nTu salud: 60/60\nLo que haces ahora: Siguiéndote\n"
            + "Bioma: Meadows\nMomento: día\nEnemigos cerca: ninguno\nTu inventario: 1 Hacha de piedra, 11 Madera\n"
            + "Otros compañeros cerca: ninguno\nTu última tarea, ya terminada: conseguir 10 de madera. Ganaste 11 Madera. Si el jugador pregunta por ella, cuéntale cómo fue o dale lo que lleves, pero no la repitas salvo que te lo pida",
            null, true);
        foreach (string question in new[] { "y la madera?", "¿Ya tienes la madera?", "Dame la madera.", "Consigue 10 más de madera." })
        {
            var sb = new StringBuilder("Tú: " + question + "\n");
            var messages = new List<ChatMessage>
            {
                new ChatMessage("system", prompt),
                new ChatMessage("user", "faltan 10 de madera"),
                new ChatMessage("assistant", "Voy a por ellas, Leif."),
                new ChatMessage("system", "Recordatorio: las órdenes de mensajes anteriores ya se atendieron; no las repitas. Atiende solo el siguiente mensaje: si es una orden, usa la herramienta de esa orden, porque tus frases no hacen nada en el juego. Si no es una orden, responde sin herramientas."),
                new ChatMessage("user", question)
            };
            LlmResponse response = Call(messages, CompanionActions.ToolDefinitions(), sb);
            foreach (ToolCall call in response?.ToolCalls ?? new List<ToolCall>()) sb.AppendLine("Acción: " + call.Name + " " + call.Arguments);
            if (!string.IsNullOrEmpty(response?.Text)) sb.AppendLine("Compañero: " + response.Text);
            Console.WriteLine(sb);
        }
    }

    private static void LiveOrders()
    {
        string prompt = PromptBuilder.Build(new CharacterProfile { Instructions = "Eres Saber, una vikinga directa y bromista." }, "", "español",
            "Tu nombre: Saber\nJugador: Leif\nLo que mira el jugador: nada en particular\nTu salud: 60/60\nLo que haces ahora: Siguiéndote\n"
            + "Bioma: Meadows\nMomento: día\nEnemigos cerca: ninguno\nTu inventario: 1 Hacha de piedra, 12 Madera\n"
            + "Tus herramientas (hacha: sí; pico: no). Sin hacha no puedes talar árboles y sin pico no puedes picar rocas; solo recoger lo suelto y romper plantas pequeñas. Tenlo en cuenta, pero menciónalo solo si te preguntan.\n"
            + "Otros compañeros cerca: Bjorn, Elsa\nTu última tarea: romper una planta. Ganaste 3 Madera",
            null, true);
        // Igual que en el juego: si hay peligro, no se le ofrecen los gestos.
        string danger = PromptBuilder.Build(new CharacterProfile { Instructions = "Eres Saber, una vikinga directa y bromista." }, "", "español",
            "Tu nombre: Saber\nJugador: Leif\nTu salud: 40/60\nLo que haces ahora: Peleando\nBioma: Meadows\nMomento: día\nEnemigos cerca: 2 Greydwarf");
        string log = Path.Combine(Path.GetTempPath(), "ocai_live.txt");

        foreach (string line in new[]
        {
            "Te quiero mucho, Saber, eres la mejor.",
            "Eres la peor compañera que he tenido.",
            "¡Hemos derrotado a Eikthyr!",
            "¿Qué opinas de Odín?",
            "¿Sabes qué hora es?",
            "Baila un poco para mí.",
            "Sígueme.",
            "Recoge 10 de piedra.",
            "!Te quiero mucho, Saber.",
            "Ve por el cofre.",
            "Ve a la cama.",
            "Ve allí.",
            "Ve hasta el campamento de los trolls.",
            "Hola, ¿cómo estás hoy?"
        })
        {
            bool inDanger = line.StartsWith("!");
            string question = line.TrimStart('!');
            var sb = new StringBuilder("Tú: " + question + (inDanger ? " (en pleno combate)" : "") + "\n");
            // Reproduce el caso real: un historial de frases sin acciones, seguido del recordatorio del mod.
            var messages = new List<ChatMessage>
            {
                new ChatMessage("system", inDanger ? danger : prompt),
                new ChatMessage("user", "Sígueme."),
                new ChatMessage("assistant", "Voy detrás de ti, Leif."),
                new ChatMessage("user", "Quédate aquí vigilando."),
                new ChatMessage("assistant", "Aquí me quedo plantada, Leif."),
                new ChatMessage("system", "Recordatorio: las órdenes de mensajes anteriores ya se atendieron; no las repitas. Atiende solo el siguiente mensaje: si es una orden, usa la herramienta de esa orden, porque tus frases no hacen nada en el juego. Si no es una orden, responde sin herramientas."),
                new ChatMessage("user", question)
            };
            string text = null;
            for (int round = 1; round <= 4; round++)
            {
                JArray tools = round <= 3 ? CompanionActions.ToolDefinitions() : null;
                LlmResponse response = Call(messages, tools, sb);
                text = response?.Text;
                if (response == null || response.ToolCalls.Count == 0) break;

                messages.Add(new ChatMessage("assistant", response.Text, response.RawToolCalls));
                bool first = round == 1;
                foreach (ToolCall call in response.ToolCalls)
                {
                    ActionRequest action = CompanionActions.Parse(call.Name, call.Arguments, out string error);
                    sb.AppendLine("Acción (ronda " + round + "): " + call.Name + " " + call.Arguments + (action == null ? " RECHAZADA: " + error : ""));
                    bool queue = action != null && CompanionActions.IsExclusiveTask(action.Kind) && !first;
                    if (action != null && CompanionActions.IsExclusiveTask(action.Kind)) first = false;
                    string result = action == null ? "Error: " + error : (queue ? "Orden en cola: " : "Orden aceptada: ") + action.Description + ".";
                    // Resultados que en el juego dependen del mundo: un dato consultado y una orden rechazada.
                    if (call.Name == "check_skills") result += " Saber: Pesca 12, Talar 34. Bjorn: Pesca 41. Elsa: Pesca 3.";
                    if (call.Name == "cook_food") result = "Error: No hay ninguna estación de cocina cerca.";
                    messages.Add(new ChatMessage("tool", result, null, call.Id));
                }
                messages.Add(new ChatMessage("system", PromptBuilder.AfterOrders));
            }
            string spoken = EmoteCatalog.Extract(text, out string emote);
            if (emote != null) sb.AppendLine("Gesto: " + emote);
            sb.AppendLine("Compañero: " + (text == null ? "(sin texto)" : LlmProtocol.Shorten(LlmProtocol.StripNarration(spoken), 220)));
            File.AppendAllText(log, sb + "\n", new UTF8Encoding(false));
        }
        Console.WriteLine("Conversación escrita en " + log);
    }

    private static CarriedStack Carry(string item, int count)
    {
        return new CarriedStack { Item = item, Count = count, MaxStack = 50 };
    }

    // Lo que ya tiene el cofre está en pilas completas, salvo que la prueba diga otra cosa.
    private static ChestSpace Chest(int id, float distance, int freeSlots, params string[] holds)
    {
        var chest = new ChestSpace { Id = id, Distance = distance, FreeSlots = freeSlots };
        foreach (string item in holds) chest.Holds.Add(item);
        return chest;
    }

    private static bool EarlyGameAvoids(float health, float rawHit, int count, int allies = 2)
    {
        float hit = ThreatAssessment.HitOn(rawHit, 1, 10f);
        return ThreatAssessment.TooStrong(health * count, hit * count, hit, 100f, 30f, allies, ThreatAssessment.DefaultLimit);
    }

    private static bool ReactsAtOnce(ReactionKind kind)
    {
        var policy = new ReactionPolicy();
        policy.NotifySpoke(100f);
        policy.Add(new Reaction { Kind = kind }, 100f);
        return policy.TryTake(101f, 180f, 10) != null;
    }

    private static string Apply(CompanionMemory memory, string summary)
    {
        memory.ApplySummary(summary, 0);
        return memory.Summary;
    }

    private static void CheckMemory()
    {
        var m = new CompanionMemory();
        for (int i = 0; i < 6; i++) m.Add("p" + i, "r" + i);
        Check("guarda pares", m.Recent.Count.ToString(), "12");
        Check("últimos mensajes: cuántos", m.ToMessages(4).Count.ToString(), "4");
        Check("últimos mensajes: orden", m.ToMessages(4)[0].Content, "p4");
        Check("últimos mensajes: cero", m.ToMessages(0).Count.ToString(), "0");
        Check("todavía no resume", m.NeedsSummary(12).ToString(), "False");

        for (int i = 6; i < 10; i++) m.Add("p" + i, "r" + i);
        Check("ya debe resumir", m.NeedsSummary(12).ToString(), "True");
        int count = m.CountToSummarize(12);
        Check("cuenta lo antiguo", count.ToString(), "8");

        List<ChatMessage> summary = m.BuildSummaryRequest("Saber", count);
        Check("resumen incluye lo antiguo", summary[1].Content.Contains("Jugador: p0").ToString(), "True");
        Check("resumen incluye al compañero", summary[1].Content.Contains("Saber: r3").ToString(), "True");
        Check("resumen excluye lo reciente", summary[1].Content.Contains("p4").ToString(), "False");

        m.Add("nuevo", "nueva");
        m.ApplySummary("Le gusta el hidromiel.", count);
        Check("aplica el resumen", m.Summary, "Le gusta el hidromiel.");
        Check("quita solo lo resumido", m.Recent[0].Content, "p4");
        Check("conserva lo llegado después", m.Recent[m.Recent.Count - 1].Content, "nueva");
        Check("usa recuerdos previos", m.BuildSummaryRequest("Saber", 2)[1].Content.Contains("Le gusta el hidromiel.").ToString(), "True");

        var huge = new CompanionMemory();
        huge.ApplySummary(new string('a', 5000), 0);
        Check("limita el resumen", (huge.Summary.Length <= CompanionMemory.MaxSummaryLength + 3).ToString(), "True");

        var cap = new CompanionMemory();
        for (int i = 0; i < 100; i++) cap.Add("p", "r");
        Check("límite duro", cap.Recent.Count.ToString(), CompanionMemory.HardLimit.ToString());

        var edit = new CompanionMemory { Summary = "x" };
        edit.Add("p", "r");
        int before = edit.Revision;
        edit.EditSummary("  y  ");
        Check("editar limpia y conserva", edit.Summary + edit.Recent.Count, "y2");
        Check("editar cambia la revisión", (edit.Revision != before).ToString(), "True");
        edit.ClearRecent();
        Check("borrar recientes conserva recuerdos", edit.Summary + edit.Recent.Count, "y0");
        edit.Add("p", "r");
        edit.RememberFact("Hecho");
        edit.AddEvent(3, MemoryWeight.Boss, "Derrotamos a Eikthyr.");
        edit.Forget();
        Check("olvidar borra todo", edit.Summary + edit.Recent.Count + edit.Facts.Count + edit.Events.Count, "000");

        var lasting = new CompanionMemory { Summary = "Viejo." };
        lasting.Add("p", "r");
        lasting.RememberFact("No me llames Lalatina");
        lasting.ApplySummary("Luchamos juntos.\nHECHO: Quiere una casa de piedra\nhecho: Le gusta el hidromiel", 2);
        Check("resumen: no borra los hechos", lasting.Facts.Contains("No me llames Lalatina").ToString(), "True");
        Check("resumen: separa los hechos nuevos", lasting.Facts.Count.ToString(), "3");
        Check("resumen: el texto queda sin hechos", lasting.Summary, "Luchamos juntos.");
        Check("resumen: sin texto conserva el anterior", Apply(lasting, "HECHO: Otra cosa"), "Luchamos juntos.");
        lasting.ApplySummary("Algo.\nHECHO: a\nHECHO: b\nHECHO: c\nHECHO: d\nHECHO: e", 0);
        Check("resumen: pocos hechos por vez", lasting.Facts.Count.ToString(), "7");
        Check("resumen: no los repite", lasting.Facts.FindAll(f => f == "Otra cosa").Count.ToString(), "1");
        string request = lasting.BuildSummaryRequest("Saber", 0)[1].Content;
        Check("resumen: sabe lo guardado aparte", request.Contains("No me llames Lalatina").ToString(), "True");

        var lived = new CompanionMemory();
        Check("suceso: se anota", lived.AddEvent(5, MemoryWeight.Notable, "Llegamos a las Llanuras.", true).ToString(), "True");
        Check("suceso: una sola vez", lived.AddEvent(9, MemoryWeight.Notable, "Llegamos a las Llanuras.", true).ToString(), "False");
        Check("suceso: una vez al día", lived.AddEvent(5, MemoryWeight.Death, "Morí y volví a la vida.").ToString() + lived.AddEvent(5, MemoryWeight.Death, "Morí y volví a la vida.").ToString(), "TrueFalse");
        Check("suceso: otro día sí", lived.AddEvent(6, MemoryWeight.Death, "Morí y volví a la vida.").ToString(), "True");
        lived.AddEvent(7, MemoryWeight.Boss, "Derrotamos a Eikthyr.", true);
        for (int i = 0; i < 20; i++) lived.AddEvent(10 + i, MemoryWeight.Job, "Trabajo " + i);
        Check("suceso: límite", lived.Events.Count.ToString(), CompanionMemory.MaxEvents.ToString());
        Check("suceso: guarda lo importante", lived.Events.Exists(e => e.Text.Contains("Eikthyr")).ToString(), "True");
        Check("suceso: olvida el trabajo antiguo", lived.Events.Exists(e => e.Text == "Trabajo 0").ToString(), "False");
        Check("suceso: conserva el trabajo reciente", lived.Events.Exists(e => e.Text == "Trabajo 19").ToString(), "True");

        var shown = new CompanionMemory { Summary = "Resumen." };
        shown.RememberFact("Hecho uno");
        shown.AddEvent(4, MemoryWeight.Boss, "Derrotamos a Eikthyr.", true);
        string term = shown.LongTerm();
        Check("largo plazo: orden", (term.IndexOf("Hecho uno") < term.IndexOf("Día 4: Derrotamos") && term.IndexOf("Día 4: Derrotamos") < term.IndexOf("Resumen.")).ToString(), "True");
        Check("largo plazo: solo resumen", new CompanionMemory { Summary = "S" }.LongTerm(), "S");
        Check("largo plazo: vacío", new CompanionMemory().LongTerm(), "");

        MemoryTexts texts = shown.Texts();
        Check("editor: muestra los sucesos", texts.Events, "Día 4: Derrotamos a Eikthyr.");
        int beforeEdit = shown.Revision;
        shown.ApplyEdit(new MemoryTexts { Facts = texts.Facts, Events = texts.Events, Summary = texts.Summary });
        Check("editor: sin cambios no toca nada", (shown.Revision == beforeEdit && shown.Events[0].Weight == MemoryWeight.Boss).ToString(), "True");
        shown.ApplyEdit(new MemoryTexts { Facts = "Uno\n\n  Dos  \r\nUno", Events = "Día 4: Derrotamos a Eikthyr.\nDía 8: Nueva\nSin día", Summary = texts.Summary });
        Check("editor: hechos por línea", string.Join("|", shown.Facts), "Uno|Dos");
        Check("editor: conserva la importancia", shown.Events[0].Weight.ToString(), MemoryWeight.Boss.ToString());
        Check("editor: lee el día", shown.Events[1].Day + shown.Events[1].Text, "8Nueva");
        Check("editor: sin día", shown.Events[2].Day + shown.Events[2].Text, "0Sin día");
        Check("editor: cancela el resumen en curso", (shown.Revision != beforeEdit).ToString(), "True");

        string withMemory = PromptBuilder.Build(new CharacterProfile(), "P", "español", "S", "RECUERDO");
        Check("prompt con recuerdos", withMemory.Contains("RECUERDO").ToString(), "True");
        Check("prompt sin recuerdos", PromptBuilder.Build(new CharacterProfile(), "P", "español", "S").Contains("recuerdas").ToString(), "False");

        string folder = Path.Combine(Path.GetTempPath(), "ocai_memory_" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonFileStore<CompanionMemory>(folder);
            var saved = new CompanionMemory { Summary = "Ñu" };
            saved.Add("¿Qué?", "Sí");
            store.Save("abc", saved);
            string problem;
            CompanionMemory loaded = store.Load("abc", out problem);
            Check("memoria: resumen", loaded.Summary, "Ñu");
            Check("memoria: mensajes", loaded.Recent[0].Content + loaded.Recent[1].Role, "¿Qué?assistant");
            saved.RememberFact("Hecho");
            saved.AddEvent(2, MemoryWeight.Boss, "Suceso");
            store.Save("duradero", saved);
            CompanionMemory lastingLoaded = store.Load("duradero", out problem);
            Check("memoria: guarda hechos y sucesos", lastingLoaded.Facts[0] + lastingLoaded.Events[0].Text + lastingLoaded.Events[0].Weight, "HechoSuceso" + MemoryWeight.Boss);
            File.WriteAllText(store.PathFor("antigua"), "{\"Summary\":\"s\",\"Recent\":[]}");
            CompanionMemory oldFile = store.Load("antigua", out problem);
            Check("memoria: archivo de antes", oldFile.Summary + oldFile.Facts.Count + oldFile.Events.Count + (problem == null), "s00True");
            Check("memoria: no guarda la revisión", File.ReadAllText(store.PathFor("abc")).Contains("Revision").ToString(), "False");
            Check("memoria: inexistente", store.Load("nada", out problem).Recent.Count.ToString(), "0");
            File.WriteAllText(store.PathFor("rota"), "{ roto");
            Check("memoria dañada avisa", (store.Load("rota", out problem).Recent.Count == 0 && problem != null).ToString(), "True");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    private static void CheckReactions()
    {
        Reaction Biome() => new Reaction { Kind = ReactionKind.NewBiome, Description = "bioma" };
        Reaction Boss() => new Reaction { Kind = ReactionKind.BossDefeated, Description = "jefe" };
        Reaction Low() => new Reaction { Kind = ReactionKind.CompanionLowHealth, Description = "salud" };

        var p = new ReactionPolicy();
        Check("nada pendiente", (p.TryTake(0f, 180f, 10) == null).ToString(), "True");

        p.Add(Biome(), 0f);
        Check("primera reacción", p.TryTake(1f, 180f, 10)?.Description, "bioma");
        Check("no repite", (p.TryTake(2f, 180f, 10) == null).ToString(), "True");

        p.Add(Low(), 10f);
        Check("respeta la pausa", (p.TryTake(11f, 180f, 10) == null).ToString(), "True");
        p.Add(Boss(), 12f);
        Check("el jefe ignora la pausa", p.TryTake(13f, 180f, 10)?.Description, "jefe");
        Check("descarta las menos importantes", (p.TryTake(400f, 180f, 10) == null).ToString(), "True");

        var q = new ReactionPolicy();
        q.Add(Biome(), 0f);
        Check("caduca sin hablar", (q.TryTake(61f, 180f, 10) == null).ToString(), "True");

        var r = new ReactionPolicy();
        r.NotifySpoke(100f);
        r.Add(Biome(), 110f);
        Check("la conversación cuenta como hablar", (r.TryTake(111f, 180f, 10) == null).ToString(), "True");

        var limit = new ReactionPolicy();
        limit.Add(Boss(), 0f);
        Check("límite: primera", limit.TryTake(0f, 0f, 1)?.Description, "jefe");
        limit.Add(Boss(), 1f);
        Check("límite por hora, incluso jefes", (limit.TryTake(1f, 0f, 1) == null).ToString(), "True");
        limit.Add(Boss(), 3601f);
        Check("el límite se renueva", limit.TryTake(3601f, 0f, 1)?.Description, "jefe");

        var order = new ReactionPolicy();
        order.Add(Biome(), 0f);
        order.Add(Low(), 0f);
        Check("elige la más importante", order.TryTake(0f, 0f, 10)?.Description, "salud");

        Check("jefe conocido", ReactionPolicy.BossName("defeated_eikthyr"), "Eikthyr");
        Check("clave ajena", ReactionPolicy.BossName("nomap"), null);
        Check("bioma conocido", ReactionPolicy.BiomeName("BlackForest"), "el Bosque Negro");
        Check("bioma None", ReactionPolicy.BiomeName("None"), null);
    }

    private static void Check(string name, string actual, string expected)
    {
        if (actual == expected) return;
        _failed++;
        Console.WriteLine("FALLO " + name + ": esperado [" + expected + "], obtenido [" + actual + "]");
    }

    /// <summary>Dos resúmenes seguidos y una pregunta, con el modelo de verdad: pocos céntimos de saldo.</summary>
    private static void LiveLastingMemory()
    {
        var log = new StringBuilder();
        var memory = new CompanionMemory { Summary = "Soy Grimhild, antigua pirata, y acompaño a Leif." };
        memory.Add("Mi hija se llama Elsa y me prometiste enseñarle a navegar.", "Lo prometí, y cumplo lo que prometo.");
        memory.Add("No me llames capitán, llámame Leif.", "Como quieras, Leif.");
        memory.Add("Ve a talar madera.", "Voy.");
        memory.Add("¡Cuidado, nos ataca un troll!", "¡Ese bicho no pasa de aquí!");
        LlmResponse first = Call(memory.BuildSummaryRequest("Grimhild", memory.Recent.Count), null, log);
        Console.WriteLine("Primera respuesta:\n" + (first?.Text ?? "(sin texto)") + "\n");
        memory.ApplySummary(first?.Text, memory.Recent.Count);
        Console.WriteLine("Hechos: " + string.Join(" | ", memory.Facts) + "\nResumen: " + memory.Summary + "\n");

        memory.Add("Qué tarde más tranquila.", "Demasiado tranquila, Leif.");
        memory.Add("Dame la madera.", "Ahí la tienes.");
        LlmResponse second = Call(memory.BuildSummaryRequest("Grimhild", memory.Recent.Count), null, log);
        memory.ApplySummary(second?.Text, memory.Recent.Count);
        Console.WriteLine("Tras el segundo resumen\nHechos: " + string.Join(" | ", memory.Facts) + "\nResumen: " + memory.Summary + "\n");

        memory.AddEvent(12, MemoryWeight.Boss, "Derrotamos a Eikthyr.", true);
        string prompt = PromptBuilder.Build(new CharacterProfile { Instructions = "Eres Grimhild, una antigua pirata sarcástica." }, "", "español",
            "Tu nombre: Grimhild\nJugador: Leif\nBioma: Meadows\nMomento: día 20, por la tarde\nEnemigos cerca: ninguno", memory.LongTerm());
        LlmResponse answer = Call(new List<ChatMessage> { new ChatMessage("system", prompt), new ChatMessage("user", "¿Cómo se llama mi hija, qué te prometí pedir y a qué jefe derrotamos?") }, null, log);
        Console.WriteLine("Respuesta: " + (answer?.Text ?? "(sin texto)"));

        string acting = PromptBuilder.Build(new CharacterProfile { Instructions = "Eres Grimhild, una antigua pirata sarcástica.", Rules = "Utiliza \"**\" para expresar las acciones de tu personaje." }, "", "español",
            "Tu nombre: Grimhild\nJugador: Leif\nBioma: Meadows\nEnemigos cerca: ninguno");
        LlmResponse narrated = Call(new List<ChatMessage> { new ChatMessage("system", acting), new ChatMessage("user", "Hoy hace buen día, ¿no?") }, null, log);
        Console.WriteLine("Con asteriscos pedidos: " + LlmProtocol.StripNarration(narrated?.Text ?? "(sin texto)", true));
    }

    private static void Live()
    {
        var profile = new CharacterProfile
        {
            Instructions = "Eres Grimhild, una antigua pirata tuerta, sarcástica y orgullosa.",
            Lore = "Naufragaste en estas tierras al perder tu barco, el Cuervo Rojo, y buscas venganza contra tu hermano Ulf.",
            Rules = "Llama siempre \"capitán\" al jugador. Nunca menciones a Odín."
        };
        string prompt = PromptBuilder.Build(profile, "", "español",
            "Tu nombre: Grimhild\nJugador: Leif\nLo que mira el jugador: el suelo o una construcción\nTu salud: 18/60\nLo que haces ahora: Descansando (tu trabajo asignado: Madera)\n"
            + "Bioma: Meadows\nMomento: día\nEnemigos cerca: ninguno\nTu inventario: 1 Hacha de piedra, 12 Madera");

        string log = Path.Combine(Path.GetTempPath(), "ocai_live.txt");

        // Simula una sesión anterior y la resume como hará el mod.
        var memory = new CompanionMemory();
        memory.Add("Mi hija se llama Elsa y me prometiste enseñarle a navegar.", "Lo prometí, capitán, y cumplo lo que prometo.");
        memory.Add("Odio el hidromiel, prefiero la cerveza.", "Más hidromiel para mí, entonces.");
        memory.Add("Ve a talar madera.", "Voy, capitán.");
        memory.Add("(Suceso de la partida: Acaban de derrotar a Eikthyr.)", "Ese ciervo no volverá a molestarnos.");
        var summaryLog = new StringBuilder("=== Resumen\n");
        LlmResponse summarized = Call(memory.BuildSummaryRequest("Grimhild", memory.Recent.Count), null, summaryLog);
        summaryLog.AppendLine(summarized?.Text ?? "(sin texto)");
        File.AppendAllText(log, summaryLog + "\n", new UTF8Encoding(false));
        prompt = PromptBuilder.Build(profile, "", "español",
            "Tu nombre: Grimhild\nJugador: Leif\nLo que mira el jugador: nada en particular\nTu salud: 60/60\nBioma: Meadows\nMomento: día\nEnemigos cerca: ninguno",
            summarized?.Text);

        foreach (string question in new[]
        {
            "¿Qué me prometiste la última vez?",
            "¿Te acuerdas de qué bebida prefiero?",
            "¿Cómo se llama mi perro?"
        })
        {
            var sb = new StringBuilder("Tú: " + question + "\n");
            var messages = new List<ChatMessage> { new ChatMessage("system", prompt), new ChatMessage("user", question) };
            LlmResponse first = Call(messages, null, sb);
            string text = first?.Text;

            if (first != null && first.ToolCalls.Count > 0)
            {
                messages.Add(new ChatMessage("assistant", first.Text, first.RawToolCalls));
                foreach (ToolCall call in first.ToolCalls)
                {
                    ActionRequest action = CompanionActions.Parse(call.Name, call.Arguments, out string error);
                    sb.AppendLine("Acción: " + call.Name + " " + call.Arguments + " => " + (action == null ? "RECHAZADA: " + error : action.Kind + "=" + action.Value));
                    messages.Add(new ChatMessage("tool", action == null ? "Error: " + error : "Orden aceptada: " + action.Description + ".", null, call.Id));
                }
                text = Call(messages, null, sb)?.Text;
            }
            string spoken = EmoteCatalog.Extract(text, out string emote);
            if (emote != null) sb.AppendLine("Gesto: " + emote);
            sb.AppendLine("Compañero: " + (text == null ? "(sin texto)" : LlmProtocol.Shorten(LlmProtocol.StripNarration(spoken), 220)));
            File.AppendAllText(log, sb + "\n", new UTF8Encoding(false));
        }
        Console.WriteLine("Conversación escrita en " + log);
    }

    private static LlmResponse Call(List<ChatMessage> messages, JArray tools, StringBuilder sb)
    {
        using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
        {
            http.DefaultRequestHeaders.Add("player2-game-key", "OfflineCompanionsAI");
            DateTime started = DateTime.UtcNow;
            string body = LlmProtocol.BuildRequest(messages, null, 0, 0.8f, tools);
            HttpResponseMessage res = http.PostAsync("http://localhost:4315/v1/chat/completions", new StringContent(body, Encoding.UTF8, "application/json")).Result;
            string text = res.Content.ReadAsStringAsync().Result;
            sb.AppendLine("[HTTP " + (int)res.StatusCode + " en " + (DateTime.UtcNow - started).TotalSeconds.ToString("0.0") + " s]");
            if (!res.IsSuccessStatusCode) sb.AppendLine(text);
            return LlmProtocol.ParseResponse(text);
        }
    }
}
