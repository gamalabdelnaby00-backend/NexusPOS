using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Desktop.UI;

namespace UniversalPOS.Desktop.Pages;

/// <summary>Settings: interface language and the signed-in account. Roles and login accounts have their own sidebar items.</summary>
public sealed class SettingsPage : UserControl
{
    public SettingsPage(CurrentUserSession session, Action showMyPermissions)
    {
        var stack = new StackPanel();
        stack.Children.Add(UiKit.PageTitle("Settings", UiKit.T("الإعدادات", "Settings")));

        // ---- language
        var language = UiKit.Combo(new[] { "العربية", "English" }, 220);
        language.SelectedIndex = UiKit.IsArabic ? 0 : 1;
        language.HorizontalAlignment = HorizontalAlignment.Left;
        bool initializing = true;
        language.SelectionChanged += (_, _) =>
        {
            if (initializing) return;
            AppearanceManager.Current.SetLanguage(language.SelectedIndex == 0 ? "ar" : "en");
        };
        initializing = false;
        stack.Children.Add(SectionCard(UiKit.T("اللغة", "Language"),
            UiKit.T("لغة الواجهة واتجاه العرض.", "Interface language and layout direction."), language));

        // ---- account
        var account = new StackPanel();
        account.Children.Add(Line(UiKit.T("الاسم", "Name"), session.DisplayName ?? string.Empty));
        account.Children.Add(Line(UiKit.T("اسم المستخدم", "Username"), session.UserName ?? string.Empty));
        account.Children.Add(Line(UiKit.T("الأدوار", "Roles"), string.Join(", ", session.Roles)));
        var permissions = UiKit.ActionButton("Shield", UiKit.T("صلاحياتي", "My permissions"), UiKit.Soft, UiKit.Ink, UiKit.B("#C9D9EC"));
        permissions.HorizontalAlignment = HorizontalAlignment.Left;
        permissions.Margin = new Thickness(0, 12, 0, 0);
        permissions.Click += (_, _) => showMyPermissions();
        account.Children.Add(permissions);
        stack.Children.Add(SectionCard(UiKit.T("حسابي", "My account"), UiKit.T("بيانات الحساب المسجل دخوله.", "The signed-in account."), account));

        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = stack, Padding = new Thickness(0, 0, 4, 0) };
    }

    private static UIElement Line(string label, string value)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        row.Children.Add(new TextBlock { Text = label + ":", Width = 130, FontWeight = FontWeights.SemiBold, Foreground = UiKit.Muted, FontSize = 13 });
        row.Children.Add(new TextBlock { Text = value, Foreground = UiKit.Ink, FontSize = 13 });
        return row;
    }

    private static Border SectionCard(string title, string description, UIElement body)
    {
        var inner = new StackPanel();
        inner.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = UiKit.Ink });
        inner.Children.Add(new TextBlock { Text = description, FontSize = 12, Foreground = UiKit.Muted, Margin = new Thickness(0, 3, 0, 14) });
        inner.Children.Add(body);
        var card = UiKit.Card(inner, 20);
        card.Margin = new Thickness(0, 0, 0, 16);
        return card;
    }
}
