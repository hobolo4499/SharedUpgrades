# SharedUpgrades

Upgrades are team upgrades. When any player activates an upgrade item (health, stamina,
sprint speed, strength, range, throw, extra jump, tumble launch/climb/wings, crouch rest,
map player count, death head battery), every player in the game receives it too.

## How it works
- The **host** keeps a ledger of every upgrade used this run, plus how many shared upgrades
  each player has already received.
- When someone uses an upgrade, the host records it and tells every other player to apply it.
- Players who join later, or who weren't in the previous level, get every upgrade they
  missed a few seconds after they spawn.
- The ledger is saved with the run, so it survives level changes and reloading a save.
  Starting a new run resets it.

## Requirements
- **All players must install this mod**, the host especially. Without the host, nothing is
  shared. A client without the mod won't get the immediate stat changes.

## Notes
- Only upgrades applied by using an upgrade item are shared. Upgrades a player had before
  installing the mod stay personal.
- Upgrades added by other mods through **REPOLib** are shared too (REPOLib is optional).
  For those, any level a player gains counts, including from debug commands or other mods.
  Mod upgrades that don't use REPOLib are not shared.
- If another mod already gives a level to every player at once, it still counts as one
  upgrade, so nobody gets it several times.
- Don't combine this with another upgrade-sharing mod (such as CrewUpgrades), or players
  get upgrades twice.
