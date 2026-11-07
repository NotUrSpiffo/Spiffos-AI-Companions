# Spiffo's Valheim AI Companions 0.25.6
AI-powered companion conversation, natural-language orders and long-term memory
for Valheim, with English/Spanish language selection and Offline Companions
Reborn 1.5.8 integrated.

Requires Valheim, BepInEx and an AI service. Voice input requires the Player2 app.
See the included README for installation and configuration.

## Fixes
- Combat: a companion attacked while holding a pickaxe, hammer or fishing rod
  now switches to a melee weapon (or the bow, or fists if it has neither).
  Before, in the ranged stance with the enemy close, it kept the pickaxe and
  could only block.
- Mining: parts of a rock that can only be reached by swimming are skipped,
  and so are rocks with no other parts. Before, copper and tin veins in lakes
  made the companion swim to them, unable to mine, until it ran out of stamina.

These fixes have not been tested in game yet.

Package includes the supplied DLL, source, example configurations, Spanish
translations, third-party notices and the complete Reborn MIT license.