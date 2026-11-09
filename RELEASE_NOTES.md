# Spiffo's Valheim AI Companions 0.26.2
AI-powered companion conversation, natural-language orders and long-term memory
for Valheim, with English/Spanish language selection and Offline Companions
Reborn 1.5.8 integrated.

Requires Valheim, BepInEx and an AI service. Voice input requires the Player2 app.
See the included README for installation and configuration.

## Chest sorting (0.26.2)
- Smelted metal goes to the chest that already holds it, even with two
  smelters running. Before, tin could end up in the nearest chest, and from
  then on that chest kept getting it. Between two matching chests, the one
  holding more of that metal wins.
- Honey, other leftovers, gathered materials and harvested crops go to a
  chest with the same item or, failing that, to the one with the most items
  of the same type: food, ore, wood, stone, gear or trophies. Items from
  other mods fall into their type from the game's own data.

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