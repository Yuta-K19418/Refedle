# hero.gif - the TUI half of the workflow: explore a CSV, add one filter, save the recipe.
# Run via docs/vhs/regen.sh (it stages the sample data and a `refedle` shim on PATH).

Output hero.gif

Set Shell bash
Set FontSize 15
Set Width 1000
Set Height 640
Set Padding 12
Set Margin 0
Set Theme { "name": "navy", "background": "#0c1a2b", "foreground": "#c8d3e6", "selection": "#1c3a5e", "cursor": "#8fb6e0", "black": "#0c1a2b", "red": "#f7768e", "green": "#9ece6a", "yellow": "#e0af68", "blue": "#7aa2f7", "magenta": "#bb9af7", "cyan": "#7dcfff", "white": "#c8d3e6", "brightBlack": "#3b4d68", "brightRed": "#f7768e", "brightGreen": "#9ece6a", "brightYellow": "#e0af68", "brightBlue": "#7aa2f7", "brightMagenta": "#bb9af7", "brightCyan": "#7dcfff", "brightWhite": "#e8eefc" }
Set Framerate 18
Set TypingSpeed 45ms

Hide
Type 'export PS1="\[\e[38;5;150m\]$ \[\e[0m\]"' Enter
Type "rm -f people.yaml" Enter
Type "clear" Enter
Show

Sleep 400ms
Type "refedle --file people.csv" Sleep 150ms Enter
Sleep 1100ms

# --- explore: scroll through the table ---
Type "j" Sleep 150ms
Type "j" Sleep 150ms
Type "l" Sleep 250ms
Type "d" Sleep 500ms
Type "u" Sleep 450ms

# --- move to the age column, keep only rows where age is 30 ---
Type "h" Sleep 100ms
Type "h" Sleep 100ms
Type "h" Sleep 100ms
Type "l" Sleep 450ms
Type "x" Sleep 700ms
Type "j" Sleep 170ms
Type "j" Sleep 170ms
Type "j" Sleep 300ms
Enter Sleep 800ms
Enter Sleep 450ms
Type "j" Sleep 350ms
Enter Sleep 450ms
Type "30" Sleep 400ms
Enter Sleep 1100ms

# --- save the recipe (ends on the "saved" confirmation) ---
Type "s" Sleep 400ms
Type "people.yaml" Sleep 450ms
Enter Sleep 900ms
Sleep 2400ms
