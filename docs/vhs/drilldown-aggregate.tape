# drilldown-aggregate.gif - scan a whole JSON array and aggregate it into a table.
# Run via docs/vhs/regen.sh.

Output drilldown-aggregate.gif

Set Shell bash
Set FontSize 15
Set Width 1000
Set Height 600
Set Padding 12
Set Margin 0
Set Theme { "name": "navy", "background": "#0c1a2b", "foreground": "#c8d3e6", "selection": "#1c3a5e", "cursor": "#8fb6e0", "black": "#0c1a2b", "red": "#f7768e", "green": "#9ece6a", "yellow": "#e0af68", "blue": "#7aa2f7", "magenta": "#bb9af7", "cyan": "#7dcfff", "white": "#c8d3e6", "brightBlack": "#3b4d68", "brightRed": "#f7768e", "brightGreen": "#9ece6a", "brightYellow": "#e0af68", "brightBlue": "#7aa2f7", "brightMagenta": "#bb9af7", "brightCyan": "#7dcfff", "brightWhite": "#e8eefc" }
Set Framerate 18
Set TypingSpeed 45ms

Hide
Type 'export PS1="\[\e[38;5;150m\]$ \[\e[0m\]"' Enter
Type "clear" Enter
Show

Sleep 400ms
Type "refedle --file orders.json" Sleep 150ms Enter
Sleep 1100ms

# --- glance at the records, then aggregate the whole array into a table ---
Type "j" Sleep 450ms
Type "j" Sleep 450ms
Type "k" Sleep 450ms
Type "x" Sleep 800ms
Enter Sleep 2600ms
