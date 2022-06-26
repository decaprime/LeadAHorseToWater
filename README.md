# 🐴 Lead A Horse To Water

A V-Rising mod that lets your horses drink water from wells.

## Motivation
<img src="https://user-images.githubusercontent.com/62450933/175367019-be27ef84-4676-45cc-809c-41e7244d3594.png" width="300" />

> *Bye, bye, Li'L Sebastian* <br>
> *Miss you in the saddest fashion* <br>
> *You're 5000 candles in the wind*

## Configurable Values
```ini
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

## If true will rename horses in drinking range with the DrinkingPrefix
# Setting type: Boolean
# Default value: true
EnableRename = true

## If true use a different color for the DrinkingPrefix
# Setting type: Boolean
# Default value: true
EnablePrefixColor = true

## Prefix to use on horses that are drinking
# Setting type: String
# Default value: [Drinking] 
DrinkingPrefix = [Drinking]
```

## Demo Video (only viewable on github)
https://user-images.githubusercontent.com/62450933/175365529-f6ade327-dbd0-4500-b840-128ac52cefe7.mp4