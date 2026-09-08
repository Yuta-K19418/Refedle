# apply.gif - the CLI half of the workflow: replay the saved recipe against a file, no UI.
# Run via docs/vhs/regen.sh (it stages the sample data and a `refedle` shim on PATH).

Output apply.gif

Set Shell bash
Set FontSize 15
Set Width 1000
Set Height 600
Set Padding 12
Set Margin 0
Set Theme { "name": "navy", "background": "#0c1a2b", "foreground": "#c8d3e6", "selection": "#1c3a5e", "cursor": "#8fb6e0", "black": "#0c1a2b", "red": "#f7768e", "green": "#9ece6a", "yellow": "#e0af68", "blue": "#7aa2f7", "magenta": "#bb9af7", "cyan": "#7dcfff", "white": "#c8d3e6", "brightBlack": "#3b4d68", "brightRed": "#f7768e", "brightGreen": "#9ece6a", "brightYellow": "#e0af68", "brightBlue": "#7aa2f7", "brightMagenta": "#bb9af7", "brightCyan": "#7dcfff", "brightWhite": "#e8eefc" }
Set Framerate 18
Set TypingSpeed 28ms

Hide
Type 'export PS1="\[\e[38;5;150m\]$ \[\e[0m\]"' Enter
Type "rm -f people.out.csv" Enter
Type "clear" Enter
Show

Sleep 400ms
Type "cat people.yaml" Sleep 120ms Enter
Sleep 2000ms

Type "# preview the plan, write nothing" Sleep 120ms Enter
Type "refedle apply --input people.csv --recipe people.yaml --dry-run" Sleep 120ms Enter
Sleep 2600ms

Type "# apply it" Sleep 120ms Enter
Type "refedle apply --input people.csv --recipe people.yaml --output people.out.csv" Sleep 120ms Enter
Sleep 1800ms

Type "# open the result: every row matches the recipe" Sleep 120ms Enter
Type "refedle --file people.out.csv" Sleep 120ms Enter
Sleep 1100ms
# End on the result table (age == 30 for every row). No quit - VHS stops here.
Sleep 2400ms
