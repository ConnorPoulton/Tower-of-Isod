INCLUDE Ink_Template.ink

->entry

=== entry ===
you have a kid score of {GetIntVariable("Kid_Score")} 
+[go to Test_02]
~SetStringVariable("Next_Scene","Test_02")
->END
+[increase Kid Score]
~SetIntVariable("Kid_Score", GetIntVariable("Kid_Score") + 2)
->entry
+[Does Kid like me?]
{{GetIntVariable("Kid_Score")} > 2 : No | Yes}
->entry