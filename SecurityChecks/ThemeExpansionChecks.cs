using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using LootDrop.Services;
using LootDrop.Views;

internal static class ThemeExpansionChecks
{
    public static void Run(Action<bool,string> check,string root,bool screenshots)
    {
        var library=new LocalLibraryService(Path.Combine(root,"theme-font-expansion"));
        var window=new MainWindow(library,false);window.Show();
        void Settle(){Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();}
        void Save(string name){if(!screenshots)return;using var image=window.CaptureRenderedFrame();image!.Save(Path.Combine(AppContext.BaseDirectory,"theme-screenshots",name+".png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());}
        var fonts=window.FindControl<ComboBox>("FontChoiceBox")!;
        var additional=FontCatalog.Choices.Where(c=>new[]{"lato","montserrat","source","plex","atkinson"}.Contains(c.Key)).ToArray();
        check(additional.Length==5 && FontCatalog.Choices.Select(c=>c.Key).Distinct().Count()==9,"Five new font keys are unique and preserve the existing choices");
        foreach(var font in FontCatalog.Choices.Where(c=>c.LicenseStem!=null))
            check(window.FindControl<TextBlock>("FontLicenseText")!.Text!.Contains(font.Name+"\n"),font.Name+": complete bundled license is listed in About");
        var compact=typeof(MainWindow).GetMethod("SetDockAppearance",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        compact.Invoke(window,new object[]{true});window.Width=360;window.Height=900;
        foreach(var font in additional)
        {
            fonts.SelectedItem=font;
            foreach(var theme in new[]{"Paper Ink","Deep Ocean"})
            {
                window.FindControl<ComboBox>("DockNavigationBox")!.SelectedIndex=8;Settle();
                window.GetLogicalDescendants().OfType<Button>().Single(b=>b.Tag as string==theme).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.FindControl<ComboBox>("DockNavigationBox")!.SelectedIndex=7;
                window.FindControl<ComboBox>("SettingsCategoryBox")!.SelectedIndex=3;Settle();
                check(fonts.Bounds.Width>0 && fonts.Bounds.Width<=window.ClientSize.Width && window.FindControl<StackPanel>("SettingsPage")!.Bounds.Width<=360,$"{font.Name}/{theme}: font selection fits the 360px sidebar");
                check(library.Load().AppFontKey==font.Key && library.Load().ThemeName==theme,$"{font.Name}/{theme}: color palette and font selections persist independently");
                Save("Compact-"+font.Key+"-"+theme.Replace(' ','-'));
            }
            var restored=new MainWindow(library,false);
            check(restored.FontFamily.Name==font.FamilyName,font.Name+": new font survives a fresh window load");restored.AllowExit();restored.Close();
        }
        compact.Invoke(window,new object[]{false});window.Width=1280;window.Height=900;fonts.SelectedItem=FontCatalog.Choices[0];
        window.FindControl<Button>("NavThemes")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle();
        var preview=window.GetLogicalDescendants().OfType<Button>().Single(b=>b.Tag as string=="Neon Voltage");preview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));preview.BringIntoView();Settle();Save("Themes-0.7.14-Preview");
        window.FindControl<Button>("NavSettings")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.FindControl<StackPanel>("SettingsSubmenu")!.Children.OfType<Button>().Single(b=>b.Tag as string=="appearance").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));fonts.SelectedItem=FontCatalog.Choices.Single(c=>c.Key=="source");Settle();Save("Fonts-0.7.14-Preview");
        window.AllowExit();window.Close();
    }
}
