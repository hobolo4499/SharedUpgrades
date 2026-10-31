# Changelog

## 1.2.0
- Added: upgrades from other mods that use REPOLib (for example extra upgrade mods) are now
  shared with the whole team as well. REPOLib is not required.
- Fixed: when another mod gives an upgrade to every player at once, players no longer
  get it once per player.
- Changed: shared upgrades now reach the other players after a short delay (about 1 second),
  so a level another mod already gave everyone isn't handed out twice.

## 1.1.1
- Changed: the mod now uses the SolEclips name everywhere, matching its Thunderstore page.
- Added: a Website link on the Thunderstore page to the source code on GitHub, where you
  can also report bugs.

## 1.1.0
- Changed: the host now keeps track of every upgrade used and hands them out to all players.
- Added: players who join later, or who missed a level, get every upgrade they missed.
- Added: shared upgrades are saved with the run.

## 1.0.0
- First release: when a player uses an upgrade, every player gets it.
