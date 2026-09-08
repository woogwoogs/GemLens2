# GemLens2

A small skill gem reference window to compare gem level and info without having to alt tab / look at 2nd monitor. 

You can

- Hover over a gem and hit the keybind to directly open the info on that gem at its current level
- You can also just open the reference window normally and search for gems or click the my gems tab to see your active skill gems



Current data is an up to date snapshot. I excluded the auto updating of the database as not much changes throughout the league and didnt want to include python scripts in the plugin. I'll update whenever there is a change to a gem on github



## My skills details

Reads `GameController.Player.GetComponent<Actor>().ActorSkills` and includes entries marked as user skills or present on the skill bar, requiring `ActiveSkill.IsGem` and excluding support effects. Names come from `EffectsPerLevel.GrantedEffect.ActiveSkill.DisplayName`, falling back to the actor skill name. Levels come from `EffectsPerLevel.Level`.


