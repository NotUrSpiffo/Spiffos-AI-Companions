# Spiffo's Valheim AI Companions 0.27.2
AI-powered companion conversation, natural-language orders and long-term memory
for Valheim, with English/Spanish/Russian language selection and Offline Companions
Reborn 1.5.8 integrated.

Requires Valheim, BepInEx and an AI service. Voice input requires the Player2 app.
See the included README for installation and configuration.

## Changes in 0.27.2
- Sharing in /freetalk: when companions talk among themselves, the one
  answering can hand the other items from its backpack, such as arrows,
  food or potions. Nothing makes it share: it decides as its character
  would, by how it gets along with the other and what they have lived
  through, and it may refuse, haggle or give less. A chat line says what
  was handed over, and both remember it.
- Farm zones: a companion with no farm zones of its own now uses the ones
  set on your other companions. Alt + Z opens the zones of the companion
  nearest to you, and its name now shows at the top of the editor. Tilled
  ground outside the zones is only used when no zones are set at all, so a
  companion no longer plants on tilled ground by the door while your zones
  sit elsewhere.
- Reviving: telling a companion to use a dead companion's tombstone now
  revives that companion at its tombstone instead of looting it.
- Gathering: an order for one item, such as fine wood or core wood, now
  keeps the item you named even if the AI picked another, and the
  companion only goes for trees or rocks that drop it.
- Far from the player: Valheim only runs the area around you, and its edge
  can be as close as 64 m. Gathering and hunting no longer pick trees,
  rocks, prey or search spots near that edge, prey that runs toward it is
  let go, and an order with a target amount waits instead of running out
  while the companion is out of that area. Companion animations also keep
  playing at a distance.
- Hunting: companions skip prey they consider too strong for them, start
  sneaking from 32 m (was 20 m) so deer do not see them first, and a hunt
  with no amount ends with a summary of what they brought back.

## Fixes in 0.27.1
- Farming: storing the harvest in a chest no longer freezes the game.
  When the companion carried anything it was not going to store, such as
  the crops it keeps as seed, the store loop never ended and Valheim
  stopped responding right after "Opened output chest".
- Shared chests: when two companions store things in the same chest at
  once, the second one now waits its turn (up to 8 seconds) instead of
  picking another chest or giving up. Before, the harvest, smelted metal,
  leftovers or gathered items could end up in the wrong chest, and a
  deposit order was cancelled. A chest you have open yourself is still
  left alone.
- Tombstones: a companion that died with an empty inventory, and had no
  earlier tombstone left, got a tombstone that said to hold E to revive it
  but did nothing, and its map pin vanished. That tombstone now gets its
  own ID, and tombstones already left like that can be used to revive.

## Companions together (0.27.0)
- Hair colour: the companion creator has natural tones from grey and
  platinum through blonde, ginger and auburn to black, and a new Hair Dye
  bar with rainbow colours. At the far left of that bar there is no dye.
- Orders for several: when you speak to several companions at once
  ("Roxy and Darkness, ...", "everyone", "marquen esta casa"), the others
  answer too after the first, instead of only one of them replying. The
  order is still carried out once.
- /freetalk: companions can talk among themselves. When one speaks,
  another nearby may answer, or stay quiet. Off by default; [Charla]
  MaxTurns sets how many answers follow in a row (default 2). Each answer
  uses AI credits.

## Russian translation (0.26.3)
- Russian joins English and Spanish, added at a player's request: mod
  texts, AI prompts, Reborn menus and speech, chat commands such as
  /помощь and /язык, and orders such as «за мной» or «стой». Auto picks it
  when Valheim is in Russian. Feedback from Russian-speaking players is
  welcome.
- The translation was reviewed before release: terms follow Valheim's own
  Russian names (Бродильная бочка, Молот, Пропашник), tool descriptions
  for the AI say the same as in English and Spanish, and Russian words with
  «й» or «ё», such as «домой», match what the player types.

## Chest sorting (0.26.2)
- Smelted metal goes to the chest that already holds it, even with two
  smelters running. Before, tin could end up in the nearest chest, and from
  then on that chest kept getting it. Between two matching chests, the one
  holding more of that metal wins.
- Honey, other leftovers, gathered materials and harvested crops go to a
  chest with the same item or, failing that, to the one with the most items
  of the same type: food, ore, wood, stone, gear or trophies. Items from
  other mods fall into their type from the game's own data.
- Mining: a companion that ends up standing on top of an ore rock without
  hitting it now steps down and mines from the ground, instead of shuffling
  on top of it.

## Therzie's mods (0.26.1)
- Works alongside Therzie's Warfare, Monstrum and Armory. Without them,
  nothing changes.
- Gather orders: the AI can also ask companions for 10 pelts and meats from
  Therzie's prey (fox, razorback, black and grizzly bear, rotting elk and
  lox), using the name the game shows.
- Skills: skills added by other mods, such as Warfare's throwing skill, are
  listed when you ask about a companion's skills.
- Places: the altars of Asmodeus, Svalt and Vrykolathas, and the vegvisirs
  that point to them, are named while exploring.
- Mini-bosses: defeating Asmodeus, Svalt or Vrykolathas gets a comment and a
  memory, like a boss.
- Spanish texts now use neutral Spanish.

## Multiplayer Update (0.26.0)
- Shared speech: what your companions say is shown to other players who have
  this mod, as a speech bubble over the companion and as a chat line, only
  within the normal talking distance of Valheim's chat. Players further away
  see nothing. What you type to your companion is not sent, and neither are
  the mod's own notices. Each player chooses for their own companions with
  [Multijugador] ShareSpeech in SpiffoAiCompanions.cfg, on by default.
- Other players: the AI is told which other players are within 60 m of the
  companion, up to 5, with their name, distance, whether they are badly hurt
  and what they hold in their hands. The companion recognizes them and can
  talk about them, but it only takes orders from its own player.

## Fixes in 0.25.8
- Stone: an order to gather stone now picks up loose stones when the companion
  has no pickaxe. Before, Reborn stops picking up loose stones once anyone has
  a bronze-or-better axe or there is a forge nearby, so the companion found
  nothing to do and the order ended with 0 stone. With a pickaxe it still
  mines rocks.
- Multiplayer: a companion that walks while its model is still seated is stood
  up. In multiplayer the game that runs a companion can change to another
  player's; if that happened while it was seated, nobody stood it up and other
  players saw it sliding around seated. The game that runs it stands it up for
  everyone; other games fix it on their own screen. Companions seated on a ship
  are left alone. Each fix writes a [Sentado] line to the log.

## Changes in 0.25.7
- Stuck at work: a companion chopping or mining that goes 20 seconds without
  swinging, hitting or moving (outside combat and while not catching its
  breath) leaves that target for a while and looks for another. The log gets
  a [Recoger] line describing its state, to find out why it stopped. Seen in
  game: a companion standing on a copper deposit stopped working with no
  pickaxe in hand, and Reborn logged nothing.
- Companion limit: a world can hold at most 5 companions, counting those
  waiting to revive and those dismissed. Summoning or buying a sixth is
  refused with a message, and no coins are taken. In multiplayer, a guest
  only counts the companions loaded near them.

## Fixes in 0.25.6
- Combat: a companion attacked while holding a pickaxe, hammer or fishing rod
  now switches to a melee weapon (or the bow, or fists if it has neither).
  Before, in the ranged stance with the enemy close, it kept the pickaxe and
  could only block.
- Mining: parts of a rock that can only be reached by swimming are skipped,
  and so are rocks with no other parts. Before, copper and tin veins in lakes
  made the companion swim to them, unable to mine, until it ran out of stamina.

Package includes the supplied DLL, source, example configurations, Spanish
translations, third-party notices and the complete Reborn MIT license.