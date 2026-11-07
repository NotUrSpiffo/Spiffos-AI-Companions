# Spiffo's Valheim AI Companions (English & Bilingual Edition)

A Valheim mod that adds AI-powered conversation, decision-making, and long-term memory to companions from [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn). Talk to them in **English** or **Spanish**, by text or voice. They understand what is happening in the game, obey natural-language orders, and remember your adventures together.

It bundles **Offline Companions Reborn 1.5.8** directly inside `SpiffoAiCompanions.dll` and uses its movement, combat, and job logic, so you do **not** need to install Offline Companions Reborn separately.

## Requirements

- Valheim with [BepInEx](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
- An AI service: the [Player2](https://player2.game) app open and signed in, Ollama, LM Studio, or any OpenAI-compatible API.
- For voice input: the Player2 app, where you select your microphone and speech recognition language.

## Installation

1. Install BepInEx.
2. Copy `SpiffoAiCompanions.dll` (from `instalar/BepInEx/plugins/`) into `BepInEx/plugins/`.
3. *(Optional, for Spanish UI in Reborn menus)* Copy the `Translations` folder into `BepInEx/plugins/OfflineCompanionsReborn/`.
4. Open Player2 (or start your local LLM) and launch Valheim.

## Language Selection (`Auto` / `English` / `Spanish`)

The mod defaults to **`Auto`**, automatically matching Valheim's active language (**English**, **Español (Latinoamérica)**, or **Español (España)**). You can also force a specific language at any time without restarting the game:

- **In-game command:** Type `/language auto`, `/language en`, or `/language es` (also `/lang` or `/idioma`).
- **Character Editor (`Shift` + `Y` or `/character`):** Use the **Mod Language** buttons (`English`, `Español`, `Auto (Game)`) at the top of the Character Editor window.
- **Config file (`BepInEx/config/SpiffoAiCompanions.cfg`):** Set `Language = Auto`, `English`, or `Spanish` under `[Personaje]`.

Switching the language updates all mod UI windows, HUD notifications, Reborn menus/radial wheel, AI system prompts, built-in personality profiles, and companion replies immediately. Item names are read live from Valheim's active localization and matched against both **Latin American Spanish**, **European Spanish**, and **English** terms (e.g. *hongos* / *setas* / *mushrooms*, *pedernal* / *sílex* / *flint*, *madera fina* / *madera noble* / *fine wood*). Both English and Spanish slash commands are always accepted regardless of the active language.

## Keys & Commands

| Action | How |
|---|---|
| Talk | Type in game chat (`Enter`), or press `Y` |
| Talk by voice | Hold `H`, speak, and release |
| Edit character, backstory, rules, memory & language | `Shift` + `Y`, or type `/character` (`/personaje`) when talking |
| Show command help in-game | `/help` (`/ayuda`) |
| See what every companion is doing and where they are | `/status` (`/estado`) |
| Call all companions to your side | `/recall` or `/call` (`/llamar`) |
| Check your Player2 balance and requests made | `/balance` (`/saldo`) |
| Toggle between normal and autonomous mode | `/autonomy` (`/autonomia`) |
| Mark, list, or forget named places on the map | `/mark <name>`, `/places`, `/forget <name>` (`/marcar`, `/lugares`, `/olvidar`) |
| Change mod language (`en`, `es`, `auto`) | `/language [en\|es\|auto]` (`/lang`, `/idioma`) |

`/help`, `/status`, `/recall`, `/balance`, `/autonomy`, `/places`, `/mark`, `/forget`, and `/language` do **not** use AI credits and work even when no companion is nearby.

Anything you type in game chat is heard by the companion you are looking at or the closest one within ~80 m, and each message can use AI credits. To reach farther, shout with `/s` just like in standard Valheim chat: `/s come here` reaches up to ~120 m. Both distances can be adjusted with `TalkRange` and `ShoutRange`. If nobody hears you, the game tells you how far away your closest companion is.

When other players are in the game, a companion only responds to game chat if you say their name ("Bjorn, come here"), so your conversations with other players are not sent to the AI. You can disable game-chat listening with `TalkInGameChat` in the config.

## What You Can Ask Them

| Category | Examples |
|---|---|
| Movement | "follow me", "come here", "go there", "go there and stay", "stay here", "stop" |
| Go to nearby object | "go to the chest", "go to the chest you found", "go to the bed", or the nearest workbench, fire, boat, tree, or rock |
| What you look at | "attack that one", "chop that tree", "cut that bush", "open that door", "get on that boat" |
| Jobs | chop wood, mine, forage/gather, hunt, fish, farm, cook, smelt, repair the base |
| Specific tasks | "get 10 stones", "cook 3 cooked meat", "smelt the copper", "sort the chests" |
| Upkeep | "repair the buildings", "refuel the fires", "collect the honey", "restock the material piles" |
| Other companions | "where is Bjorn?", "who is best at fishing?", "tell everyone to come here" |
| Items | "pick up that axe", "give me what it dropped", "give me 5 wood", "drop the wood", "take wood from the chest", "grab arrows", "find food in the chests" |
| Gear & food | "eat something", "drink a mead", "equip the bow", "repair your gear", "recover your gravestone" |
| Combat | "guard this area", "switch to passive stance", "retreat below half health" |
| Base & rest | "set your home here", "expand your home radius to 80 m", "stop picking items up", "go to sleep", "sit by the fire" |
| Information | "what's nearby?", "what's in the chests?", "what did you gather?" |
| Memory | "remember that our main base is to the north" |
| Places | "mark the base here", "mark the mine over there", "go back to base", "go to the base and tend it", "take care of this place", "forget the mine" |
| Exploration | "explore around here", "scout what's nearby" |
| Group orders | "everyone, follow me", "Bjorn, chop wood" (up to 4 companions at once) |
| Renaming | "your name is Astrid now" |
| Emotes | "do a little dance", "wave at me" |

Companions also accompany their replies with emotes when appropriate, such as blowing a kiss or laughing. They only perform emotes when calm: no enemies within ~25 m, not working or on a task, and not sitting, sleeping, or swimming. You can disable this with `AllowEmotes` in the config.

You can chain tasks together: *"chop that tree and give me what it drops"*.

### Marked Places, Long-Distance Travel & Base Tending

Marked places belong to each world, are known by all your companions, and appear on the map. You can also use `/mark <name>`, `/forget <name>`, and `/places`, which cost no credits. If your companion has a home set, saying "go home" or "go to base" sends them there even if you haven't marked a place.

When sent to a place, the companion stops what they are doing and walks there to wait, navigating around water that cannot be crossed with a short swim. Outside the loaded zone around you, Valheim freezes entities, so if they get too far from you or stop making progress (15 s, or 30 s if you are right next to them), they fast-travel directly to the destination. From far away they cannot hear you, not even with `/recall`: you must walk closer to them. If they were in the middle of a task chain when fast-traveling to an unloaded zone, remaining steps are cancelled.

`/status` shows how far they are from their destination and whether they are walking, going around water, or stuck, and once they arrive, that they are waiting there—even if they are in an unloaded zone.

**"Go to the base and tend it"** is different from **"go to the base"** (which only waits). Upon arrival, the companion maintains the site on their own without using AI credits (except for the arrival comment):

- **Maintenance:** Uses Reborn's repair-and-restock job: fires, building repairs (with a hammer they carry or take from a chest near their home and put back afterwards), beehives, and material stacks around their home if nearby, or within ~30 m otherwise.
- **Gathering by inclination:** During the day, if nearby chests have fewer than 50 of an item, they go gather 10 within 50 m of the site and store them in a chest. With **Craft**, wood; with **Gathering**, wood, stone, or wild plants; with **Exploration**, wild plants; with **Hunting**, meat from animals within range. Without chests they do not gather. Without an inclination they only maintain.
- **No nagging:** Whatever they find (even if less than 10) is enough, and they rest for 5 minutes. If they find nothing, they wait 15 minutes before trying that resource again.
- **Only while you are nearby:** Valheim freezes unloaded zones (~100 m with default settings), so nothing progresses while you are far away. The mod notifies you when giving the order if you are too far, and when you walk away. When you return, they resume automatically, even across game restarts.
- **Until when:** Until you give another order such as "follow me", "come here", or "stop". If strong enemies are nearby, they stop gathering but stay at the site.

### Exploration, Hunting & Inventory Details

**"Explore around here"** sends the companion walking for up to 4 minutes across terrain they haven't visited yet, using no credits except for the final report:

- **What they discover:** Reveals the map around them with the same radius as you. Marks on the map and tells you what they find within ~50 m: crypts, caves, ruins, altars, wild chests, and copper, tin, or silver veins (up to 8 per trip, without repeating already marked spots). They remember what they found so you can ask them later.
- **How far:** Stays within ~85 m of you so they don't enter unloaded zones. If you walk, they explore alongside you and reach farther. If you move away too fast, they abort and return.

When ordered to **hunt** (with or without a target count), they look from where they stand and, if no prey is visible, roam unvisited terrain within ~85 m of you to track prey down.

When asked to **store their items**, they carry each item to a nearby chest (up to 40 m) that already contains the same item, and anything else to the nearest chest with free space, making as many trips as needed. They keep equipped gear, food, weapons, armor, and torches.

**Flint** is picked up from the ground (usually near water) rather than mined with a pickaxe, avoiding deep water where they would have to swim. If a companion gets stuck swimming without stamina for 45 seconds, they are safely teleported to the nearest dry land.

When **looking for food**, they take 20% of the available stock of up to 3 different foods (the best ones across nearby chests, always at least 1). When **cooking**, they use all free, lit cooking stations nearby without exceeding the requested amount. When done, they store the food in a chest that already holds it, and do the same with metal or coal when smelting.

When you ask for a specific wood, such as fine wood or core wood, they only chop the trees that drop it. For any wood, they first finish the log of the tree they just felled, then go to whatever is closest: a standing tree, a log, or a stump.

## Autonomous Mode

By default, companions only act when asked. In **autonomous mode**, they also take the initiative: sharing thoughts, proposing plans, asking questions, and performing helpful tasks on their own. Toggle it with `/autonomy` (`/autonomia`) for all companions.

- **What they can do on their own:** Gather up to 20 of a resource, hunt, pick up loose items, take up to 20 of an item from usable chests (plus food and ammo), store gathered items, sort chests, cook up to 5 rations, eat, equip gear, repair their gear, recover their gravestone, tend the base, explore briefly, or walk over to something nearby.
- **What they never do on their own:** Attack a specific target, smelt ore, give or drop items, command other companions, or change their own settings.
- **Threat assessment (`AvoidStrongEnemies`):** Compares how long your group would take to win vs. lose based on enemy health, damage, your armor and weapon, and group size. Also avoids enemies that would kill the companion in two hits unless they go down immediately. They still fight if you or they are attacked up close or if you order it, and bosses are excluded.
- **Hunger & ammo:** If hungry without food, they check chests within ~40 m. If holding a bow or crossbow with no ammo, they fetch up to 30 arrows/bolts from chests without using AI credits.
- **Cost control:** Configurable via `IntervalSeconds` (75 s default) and `MaxPerHour` (30 default). Pauses automatically if you stand completely still for 5 minutes.

## Memory & Personality

- **Firm facts:** Names, promises, preferences, and anything you ask them to remember are stored separately from the rolling conversation summary so the AI never overwrites them (~1500 characters).
- **Lived experiences & expeditions:** Automatically records defeated bosses, deaths, close calls, raids, new biomes, completed jobs, and the first arrival at each marked place (up to 12 memories, prioritizing major events).
- **Character Editor (`/character` or `Shift` + `Y`):** View and edit their name, Mod Language, Personality Profile (6 built-in profiles), Inclination (`Automatic`, `None`, `Craft`, `Exploration`, `Hunting`, `Gathering`), custom instructions, backstory, conversation rules, firm facts, summary, and experiences.
- **Offline Companions built-in lines (`RebornSpeech`):** Set to `Avisos` (Warnings, default) so only important alerts ("I don't have an axe") appear without generic chatter talking over the AI personality.
- **Command Wheel:** Simplified to Follow, Set Home, Dismiss, and the stance ring. Farming zones remain accessible via `Alt` + `Z`.
- **Offline fallback:** If you run out of credits or lose connection, basic orders (`"come"`, `"follow me"`, `"stop"`, `"stay"`), the command wheel, and slash commands continue to work offline.

## Configuration

Located at `BepInEx/config/SpiffoAiCompanions.cfg` (created on first launch, or copy the pre-configured one from `configuracion/SpiffoAiCompanions.cfg`).

| Section | Purpose |
|---|---|
| `IA` | Provider (`Player2`, `Ollama`, `LMStudio`, `Custom`), model, API key, and timeouts |
| `Conversacion` | Keys, talk/shout range, reading speed, chat notices, and action permissions |
| `Voz` | Enable voice input, push-to-talk key, and max duration |
| `Memoria` | Save and summarize conversations across sessions |
| `Reacciones` | Automatic event comments, cooldowns, and hourly limits |
| `Personaje` | Mod language (`Language`), reply language (`ReplyLanguage`), shared personality, Reborn speech mode, and reply length |
| `Autonomia` | Normal vs. autonomous mode, interval, hourly limit, and strong-enemy avoidance |

Example with Ollama:

```ini
[IA]
Provider = Ollama
Model = llama3.2
```

*(Note: The chosen model must support tool/function calling to execute in-game orders. Voice input still requires the Player2 app.)*

## Troubleshooting

| Issue | What to check |
|---|---|
| Companion doesn't reply | Make sure the Player2 app is open and signed in (or your local LLM is running) |
| Companion talks but doesn't obey | Check the `"Order for…"` lines in chat, and ensure `AllowActions = true` |
| `"Get stones"` does nothing | Make sure they have a pickaxe, or there are loose stones nearby |
| `"Repair"` or `"restock"` doesn't work | Make sure they carry a hammer or there is one in a chest within ~30 m of their home, that they are near that home with a free slot in their pack, and that those tasks are enabled in `RepairAndRestock` in the Offline Companions `.cfg` |
| `"Fill the smelters"` does nothing | Make sure there is ore, wood, or fuel in nearby chests. They keep tending the smelters until you tell them to stop |
| Mod doesn't load | Check `BepInEx/LogOutput.log` for `Connected to Offline Companions Reborn` (`Conectado con Offline Companions Reborn`) |

## Credits & License

Includes Offline Companions Reborn by Maddhattr, based on Offline Companions by ProfMags (MIT License), integrated directly into this DLL. Not affiliated with Maddhattr, ProfMags, or Player2.
