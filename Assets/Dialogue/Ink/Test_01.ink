INCLUDE Ink_Template.ink

->entry

=== entry ===
you have a kid score of {GetIntVariable("Kid_Score")} 
the current scene is {GetStringVariable("Current_Scene")}
+[go to Test_02]
~SetNextScene("Test_02")
->END
+[increase Kid Score]
~SetIntVariable("Kid_Score", GetIntVariable("Kid_Score") + 2)
->entry
+[Does Kid like me?]
{{GetIntVariable("Kid_Score")} > 2 : No | Yes}
->entry