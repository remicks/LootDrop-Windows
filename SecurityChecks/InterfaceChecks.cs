using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LootDrop.Services;
using LootDrop.Views;

internal static class InterfaceChecks
{
    public static void RunWindow(Action<bool,string> check,string root,bool screenshots)
    {
        var folder=Path.Combine(root,"ui-fonts-settings");
        var library=new LocalLibraryService(folder);
        library.Save(new LibraryState { Games=new(){new SavedGame {Id=501,Title="A short game title",IsWishlisted=true,ClaimUrl="https://store.epicgames.com/fixture",Platforms="PC"},new SavedGame {Id=502,Title="A deliberately longer game title that wraps onto a second line",IsWishlisted=true,ClaimUrl="https://store.epicgames.com/fixture-two",Platforms="PC"}}});
        var window=new MainWindow(library,false);window.Show();
        var output=Path.Combine(AppContext.BaseDirectory,"theme-screenshots");
        void Settle(){Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();}
        void Save(string name){if(!screenshots)return;using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,name+".png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());}
        var chooser=window.FindControl<ComboBox>("FontChoiceBox")!;
        check(FontCatalog.Choices.Count==4 && FontCatalog.Resolve("unknown").Key=="outfit","Four bundled font choices and a safe default are available");
        foreach(var choice in FontCatalog.Choices)
        {
            foreach(var weight in new[]{FontWeight.Normal,FontWeight.SemiBold})
            {
                var face=new Typeface(choice.Family,FontStyle.Normal,weight).GlyphTypeface;
                Console.WriteLine($"FONT {choice.Key}: requested {weight}, actual {face.FamilyName} / {face.Weight}");
                check((face.FamilyName==choice.FamilyName || face.FamilyName.StartsWith(choice.FamilyName+" ",StringComparison.Ordinal)) && face.GlyphCount>100,$"{choice.Name}: real bundled {weight} glyphs resolve without installed fonts");
                check(weight==FontWeight.Normal ? face.Weight==FontWeight.Normal : face.Weight>=FontWeight.SemiBold,$"{choice.Name}: requested weight uses the intended static face");
            }
            chooser.SelectedItem=choice;Settle();
            check(library.Load().AppFontKey==choice.Key && window.FontFamily.Name==choice.FamilyName,$"{choice.Name}: selection applies immediately and persists");
            window.FindControl<Button>("NavWishlist")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle();
            var list=window.FindControl<ItemsControl>("OffersList")!;
            var buttons=list.GetLogicalDescendants().OfType<Button>().Where(b=>b.Content as string=="Details").ToArray();
            check(buttons.Length==2 && Math.Abs(buttons[0].TranslatePoint(default,window)!.Value.Y-buttons[1].TranslatePoint(default,window)!.Value.Y)<1,$"{choice.Name}: short and wrapped titles leave card actions aligned");
            Save("Font-"+choice.Key+"-Cards");
        }
        chooser.SelectedItem=FontCatalog.Choices[0];
        window.FindControl<Button>("NavSettings")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle();
        var submenu=window.FindControl<StackPanel>("SettingsSubmenu")!;
        check(submenu.IsVisible && submenu.Children.OfType<Button>().Count()==7,"Settings branches into seven named categories");
        check(!window.FindControl<TextBox>("SearchBox")!.IsVisible,"Settings replaces the inactive search box with a readable breadcrumb");
        var panels=new[]{"SettingsGeneralPanel","SettingsStartupPanel","SettingsDockPanel","SettingsNavigationPanel","SettingsFontPanel","SettingsAppearancePanel","SettingsLibraryPanel","UpdatesPanel","SettingsAboutPanel"};
        foreach(var button in submenu.Children.OfType<Button>())
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle();
            var key=(string)button.Tag!;
            check(panels.Count(name=>window.FindControl<Border>(name)!.IsVisible)==(key is "layout" or "appearance" ? 2:1),$"Settings/{key}: only relevant content is shown");
            Save("Settings-"+key);
        }
        submenu.Children.OfType<Button>().Single(b=>b.Tag as string=="startup").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle();
        var toggle=window.FindControl<CheckBox>("MinimizeToTrayCheck")!;
        var old=toggle.IsChecked;
        toggle.Focus(); window.KeyPress(Key.Space,Avalonia.Input.RawInputModifiers.None,PhysicalKey.Space," "); window.KeyRelease(Key.Space,Avalonia.Input.RawInputModifiers.None,PhysicalKey.Space," "); Settle();
        check(toggle.IsChecked!=old && library.Load().MinimizeToTray==(toggle.IsChecked==true),"Rounded switch retains Space-key activation and saved behavior");
        var pointer=toggle.TranslatePoint(new Point(80,toggle.Bounds.Height/2),window)!.Value;
        window.MouseDown(pointer,MouseButton.Left,RawInputModifiers.None);window.MouseUp(pointer,MouseButton.Left,RawInputModifiers.None);Settle();
        check(toggle.IsChecked==old,"Rounded switch label remains a working pointer target");
        toggle.IsChecked=true;Settle();
        var track=toggle.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="SwitchTrack");
        check(track.CornerRadius.TopLeft==12 && toggle.Bounds.Height>=40,"Preference switches use pill-shaped tracks and comfortable hit targets");
        window.FindControl<CheckBox>("LowGraphicsCheck")!.IsChecked=true;Settle();
        check(window.Classes.Contains("reduced-motion") && toggle.GetVisualDescendants().OfType<Border>().Where(b=>b.Name is "SwitchTrack" or "SwitchKnob").All(b=>b.Transitions==null),"Low-graphics mode disables switch motion");
        window.FindControl<CheckBox>("LowGraphicsCheck")!.IsChecked=false;
        var layout=typeof(MainWindow).GetMethod("SetDockAppearance",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        layout.Invoke(window,new object[]{true});window.Width=360;window.Height=900;Settle();
        var categories=window.FindControl<ComboBox>("SettingsCategoryBox")!;
        check(categories.IsVisible && !submenu.IsVisible,"Docked settings uses a compact category menu without a navigation rail");
        foreach(var index in new[]{1,2,3,5,6})
        {categories.SelectedIndex=index;Settle();check(window.FindControl<StackPanel>("SettingsPage")!.Bounds.Width<=360,"Docked category content stays within the narrow pane");Save("Docked-Settings-"+index);}
        layout.Invoke(window,new object[]{false});window.Width=1280;window.Height=900;Settle();
        window.FindControl<Button>("NavThemes")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle();
        window.FindControl<CheckBox>("LowGraphicsCheck")!.IsChecked=true;Settle();
        foreach(var theme in new[]{"Gold","Mint Sorbet"})
        {
            window.GetLogicalDescendants().OfType<Button>().Single(b=>b.Tag as string==theme).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.FindControl<Button>("NavSettings")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            submenu.Children.OfType<Button>().Single(b=>b.Tag as string=="startup").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle();
            Save("Settings-startup-"+theme.Replace(' ','-'));
            var sliderThumb=window.FindControl<Slider>("WindowOpacitySlider")!.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Thumb>().Single();
            check(sliderThumb.Background is SolidColorBrush thumbBrush && thumbBrush.Color==((SolidColorBrush)window.Resources["AccentBrush"]!).Color,"Slider thumbs follow the active theme instead of the system blue");
            check(track.Background is SolidColorBrush brush && brush.Color==((SolidColorBrush)window.Resources["AccentBrush"]!).Color,"Rounded switch tracks the selected theme accent");
            window.FindControl<Button>("NavThemes")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle();
        }
        check(window.FindControl<TextBlock>("FontLicenseText")!.Text!.Contains("SIL OPEN FONT LICENSE"),"Font license texts ship inside the app and remain readable in About");
        window.AllowExit();window.Close();
        var restored=new MainWindow(new LocalLibraryService(folder),false);
        check(restored.FontFamily.Name=="Outfit","Font choice survives a fresh window load");restored.AllowExit();restored.Close();
        var transfer=new LibraryTransferService(library);var export=transfer.Export();
        var target=new LocalLibraryService(Path.Combine(root,"font-import"));target.Save(new LibraryState {AppFontKey="sora"});var destination=new LibraryTransferService(target);
        var imported=destination.Apply(destination.PreviewExport(export));check(imported.AppFontKey=="sora","Importing a collection preserves the destination font preference");
        try{LocalLibraryService.Validate(new LibraryState{AppFontKey="https://untrusted.test/font.ttf"});check(false,"Untrusted font key rejected");}catch(InvalidDataException){check(true,"Font preferences cannot reference remote or arbitrary local files");}
    }
}
