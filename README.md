# 🐴 Lead A Horse To Water

A V-Rising mod that lets your horses drink water from wells. Now with horse breeding and other commands.

## Motivation
<img src="https://user-images.githubusercontent.com/62450933/175367019-be27ef84-4676-45cc-809c-41e7244d3594.png" width="300" />

> *Bye, bye, Li'L Sebastian* <br>
> *Miss you in the saddest fashion* <br>
> *You're 5000 candles in the wind*


# Commands and Other Features

For commands to load you must have [VampireCommandFramework](https://github.com/decaprime/VampireCommandFramework) installed on the server as well.

**Notes**:
- Commands with `[horse=]` are optionally for specifying a horse by name and will default to the closest horse if not specified.
- 🔒 Requires admin permissions
- <ins>_Underlined_</ins> keys come from the config file.
- Horses should be tamed and not riden by a player for most commands

### This info is also **available in game** with `.help LeadAHorseToWater`

---

#### `.horse breed`

This process takes two horses and consumes <ins>_BreedingRequiredItem_</ins> * <ins>_BreedingCostAmount_</ins> from the player's inventory. The resulting horse will be a random mix of the two parents' stats as a 50/50 chance of inheriting each trait from either parent. Then randomly +/- <ins>_MutationRange_</ins> is applied based on the max stat for each attribute. Finally values are capped at <ins>_MaxSpeed_</ins>, <ins>_MaxAcceleration_</ins>, <ins>_MaxRotation_</ins>. The resulting horse will be named after the first parent.

![image](https://user-images.githubusercontent.com/62450933/190880543-92d31267-34ec-4292-bb03-b12feee5a95b.png)

#### `.horse tag-stats [horse=]`

This player command will add the current stats of the horse to it's name as a suffix. This is great to see the stats of a horse without having to open the inventory. This command may limited by name length as the game allows one extra character via their UI.

![image](https://user-images.githubusercontent.com/62450933/190880667-fac067fe-764b-4e89-a059-f37ee8221fe1.png)

#### 🔒 `.horse rename [horse=] (newName)`

Powerful admin rename, this allows you do escape normal naming restrictions and use markup. This is useful for special rewards like making a horse's name color, bold, or even use some emojis or unicode.
**PSA**: This command can result in the drinking prefix breaking or tag-stats not fitting on the horse. Please don't report naming issues resulting from this command.

#### 🔒 `.horse whistle [horse=]` / `.horse warp [horse=]`

Whistle tries to brings the horse to you, warp teleports you to the horse.

#### 🔒 `.horse speed [horse=] (speed)`
#### 🔒 `.horse acceleration [horse=] (acceleration)` 
#### 🔒 `.horse rotation [horse=] (rotation)`

Set the horse's stats. These values are **not** capped by <ins>_MaxSpeed_</ins>, <ins>_MaxAcceleration_</ins>, or <ins>_MaxRotation_</ins>. Note that the game represents rotation as 10x the value displayed in the UI but the commands handle this for you and you should refer to the values as you see them in the UI.

#### 🔒 `.horse kill [horse=]`

Removes the horse immediately without any loot or corpse.

#### 🔒 `.horse cull [radius=5] [percentage=1]`

**WARNING**: This command will remove horses within the radius of the player. It choose `percentage` of the horses within the `radius`. This command is very useful for cleaning up a large number of horses. It is recommended to use a small radius to start. The default radius of 5 is about 1 tile. The default percentage 1 means 100% of the horses within the radius will be removed.

#### 🔒 `.horse spawn [count=1]`

Spawns either one or `count` horses around you.

# Configurable Values
```ini
[Breeding]

## Enables the cooldown for breeding horses.
# Setting type: Boolean
# Default value: true
EnableBreedingCooldown = true

## This is the cooldown in seconds for breeding horses.
# Setting type: Int32
# Default value: 600
BreedingCooldown = 600

## This prefab is consumed as a cost to breed horses.
# Setting type: Int32
# Default value: -570287766
BreedingRequiredItem = -570287766

## This is the name of the required item that will be consumed.
# Setting type: String
# Default value: special fish
BreedingCostItemName = special fish

## This is the amount of the required item consumed.
# Setting type: Int32
# Default value: 1
BreedingCostAmount = 1

## This is the half range +/- this value for applied for mutation.
# Setting type: Single
# Default value: 0.05
MutationRange = 0.05

## The absolute maximum speed for horses including selective breeding and mutations.
# Setting type: Single
# Default value: 14
MaxSpeed = 14

## The absolute maximum rotation for horses including selective breeding and mutations.
# Setting type: Single
# Default value: 16
MaxRotation = 16

## The absolute maximum acceleration for horses including selective breeding and mutations.
# Setting type: Single
# Default value: 9
MaxAcceleration = 9

[Server]

## Horses must be within this distance from well. (5 =1 tile)
# Setting type: Single
# Default value: 5
DistanceRequired = 5

## How many seconds added per drink tick (~1.5seconds), default values would be about 24 minutes for the default max amount at fountain.
# Setting type: Int32
# Default value: 30
SecondsDrinkPerTick = 30

## Time in seconds, default value is roughly amount of time when you take wild horses.
# Setting type: Int32
# Default value: 28800
MaxDrinkAmount = 28800

## If true will rename horses in drinking range with a symbol
# Setting type: Boolean
# Default value: true
EnableRename = true

## This is a comma seperated list of prefabs to use for the well. You can choose from one of (stone, iron, bronze, small, big) or (advanced: at your own risk) you can also include an arbitrary guid hash of of a castle connected placeable.
# Setting type: String
# Default value: Stone, Large
EnabledWellPrefabs = Stone, Large
```

# Demo Video (only viewable on github)
https://user-images.githubusercontent.com/62450933/175365529-f6ade327-dbd0-4500-b840-128ac52cefe7.mp4