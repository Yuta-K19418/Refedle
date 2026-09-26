// Suppress false-positive: Terminal.Gui types are used in this file,
// but Roslyn analyzer incorrectly reports using as unnecessary.
#pragma warning disable IDE0005
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui.Views.Dialogs;

/// <summary>
/// Extension methods for <see cref="OptionSelector{TEnum}"/> to enable auto-select and Vim key navigation.
/// </summary>
internal static class OptionSelectorExtensions
{
    /// <summary>
    /// Configures <see cref="OptionSelector{TEnum}"/> to auto-select focused option and support Vim j/k navigation.
    /// </summary>
    /// <param name="selector">The option selector to configure.</param>
    internal static void EnableAutoSelectAndVimKeys<TEnum>(this OptionSelector<TEnum> selector)
        where TEnum : struct, Enum
    {
        foreach (var cb in selector.SubViews.OfType<CheckBox>())
        {
            cb.HasFocusChanged += (_, _) =>
            {
                if (cb.HasFocus)
                {
                    var value = selector.GetCheckBoxValue(cb);
                    ((SelectorBase)selector).Value = value;
                }
            };
        }

        selector.KeyDown += (sender, key) =>
        {
            if (key == Key.J)
            {
                var maxCount = selector.SubViews.OfType<CheckBox>().Count();
                if (selector.FocusedItem < maxCount - 1)
                {
                    selector.FocusedItem++;
                    key.Handled = true;
                }

                return;
            }

            if (key == Key.K)
            {
                if (selector.FocusedItem > 0)
                {
                    selector.FocusedItem--;
                    key.Handled = true;
                }
            }
        };
    }
}
