# GemLens2

A small skill gem reference window to compare gem level and info without having to alt tab / look at 2nd monitor. 


<img width="1547" height="872" alt="Screenshot 2026-09-08 121559" src="https://github.com/user-attachments/assets/92b621f0-be93-4aa1-a8a7-87325ed034c5" />


<img width="1618" height="905" alt="Screenshot 2026-09-08 121631" src="https://github.com/user-attachments/assets/c712a081-30c1-4aa5-b849-c3a06cccea0c" />


You can

- Hover over a gem and hit the keybind to directly open the info on that gem at its current level
- You can also just open the reference window normally and search for gems or click the my gems tab to see your active skill gems



Current data is an up to date snapshot. I excluded the auto updating of the database as not much changes throughout the league and didnt want to include python scripts in the plugin. I'll update whenever there is a change to a gem on github



## My skills details

Reads `GameController.Player.GetComponent<Actor>().ActorSkills` and includes entries marked as user skills or present on the skill bar, requiring `ActiveSkill.IsGem` and excluding support effects. Names come from `EffectsPerLevel.GrantedEffect.ActiveSkill.DisplayName`, falling back to the actor skill name. Levels come from `EffectsPerLevel.Level`.


