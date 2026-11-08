# Spiffo's Valheim AI Companions 0.25.8
AI-powered companion conversation, natural-language orders and long-term memory
for Valheim, with English/Spanish language selection and Offline Companions
Reborn 1.5.8 integrated.

Requires Valheim, BepInEx and an AI service. Voice input requires the Player2 app.
See the included README for installation and configuration.

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

The 0.25.8 fixes have not been tested in game yet. They come from the 0.25.7
test, where stone orders failed and a friend's companion was seen walking
seated. In the 0.25.6 test the combat fix was not exercised and mining still
got stuck, which led to 0.25.7.

Package includes the supplied DLL, source, example configurations, Spanish
translations, third-party notices and the complete Reborn MIT license.