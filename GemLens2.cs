using System.Numerics;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements;
using ImGuiNET;

namespace GemLens2;

public sealed partial class GemLens2 : BaseSettingsPlugin<GemLens2Settings>
{
    private GemDatabase _database = new();
    private GemEntry? _gem;
    private GemEntry[] _filtered = Array.Empty<GemEntry>();
    private string _search = "", _error = "", _notice = "";
    private bool _open, _favoritesOnly, _fullRange, _scrollToLevel;
    private int _table, _stat, _from, _to, _rangeMode;
    private static readonly Vector4 Teal = new(.30f,.83f,.73f,1);
    private static readonly Vector4 Gold = new(1,.76f,.35f,1);
    private static readonly Vector4 Muted = new(.58f,.63f,.68f,1);
    private Progression? Current => _gem?.Tables.ElementAtOrDefault(_table);

    // Native formatted-text APIs interpret '%' as printf directives. Game stat strings
    // must always use TextUnformatted, including colored text, wrapping and tooltips.
    private static string Plain(string text) => text.Replace('\u2013','-').Replace('\u2014','-').Replace('\u2212','-').Replace('\u00a0',' ');
    private static void Colored(Vector4 color, string text)
    { ImGui.PushStyleColor(ImGuiCol.Text,color); ImGui.TextUnformatted(Plain(text)); ImGui.PopStyleColor(); }
    private static void Wrapped(string text)
    { ImGui.PushTextWrapPos(0); ImGui.TextUnformatted(Plain(text)); ImGui.PopTextWrapPos(); }
    private static void Tooltip(string text)
    { ImGui.BeginTooltip(); ImGui.TextUnformatted(Plain(text)); ImGui.EndTooltip(); }

    public override bool Initialise()
    {
        CanUseMultiThreading = false;
        Settings.Favorites ??= new();
        try
        {
            _database = GemDatabase.Load(DirectoryFullName);
            Select(_database.Gems.Find(g => g.Slug == Settings.LastGem) ?? _database.Gems[0]);
            Filter();
        }
        catch (Exception e) { _error = e.Message; }
        return true;
    }

    public override void Tick()
    {
        if (!Settings.Enable.Value) return;
        if (!GameController.InGame) { _mySkills.Clear(); _skillLevels.Clear(); _skillPlayer = 0; _nextSkillRead = default; return; }
        if (_skillPlayer != 0 && GameController.Player?.Address != _skillPlayer)
        { _mySkills.Clear(); _skillLevels.Clear(); _skillPlayer = 0; _nextSkillRead = default; }
        if (!GameController.Window.IsForeground()) return;
        if (!Settings.OpenKey.PressedOnce()) return;
        bool changed = false;
        _notice = "";
        if (Settings.SelectHoveredGem.Value)
        {
            try
            {
                var hover = GameController.Game.IngameState.UIHover?.AsObject<HoverItemIcon>();
                var item = hover?.Item;
                if (hover is { IsValid: true } && item is { IsValid: true })
                {
                    var skillGem = item.GetComponent<SkillGem>();
                    if (skillGem != null)
                    {
                        var match = _database.Gems.Find(g => !string.IsNullOrEmpty(g.Metadata) &&
                            string.Equals(g.Metadata, item.Path, StringComparison.OrdinalIgnoreCase));
                        if (match == null)
                        {
                            string name = item.GetComponent<Base>()?.Name ?? "";
                            var matches = _database.Gems.Where(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
                            if (matches.Length == 1) match = matches[0];
                        }
                        if (match != null)
                        {
                            changed = match != _gem;
                            Select(match, skillGem.Level);
                            _notice = $"Hovered gem: level {skillGem.Level}. Equipment bonuses are not included.";
                        }
                        else { _notice = "This gem is not in the bundled skill database. Use search to browse other skills."; changed = true; }
                    }
                }
            }
            catch { _notice = "Could not read that gem. You can still select it using search."; }
        }
        _open = changed || !_open;
    }

    private void Filter() => _filtered = _database.Gems.Where(g =>
        (!_favoritesOnly || Settings.Favorites.Contains(g.Slug)) &&
        (_search.Length == 0 || g.Name.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
         g.Category.Contains(_search, StringComparison.OrdinalIgnoreCase) || g.Weapon.Contains(_search, StringComparison.OrdinalIgnoreCase))).ToArray();

    private void Select(GemEntry gem, int level = 20)
    {
        _selectedPlayerSkill = null;
        _scrollToLevel = true;
        _gem = gem; Settings.LastGem = gem.Slug; _table = 0; _stat = 0;
        ResetLevels(level);
    }

    private void ResetLevels(int level)
    {
        if (Current is not { } t) return;
        _from = t.Nearest(level); _to = Math.Min(_from + 1, t.Levels.Length - 1);
        _stat = Math.Max(0,t.Columns.FindIndex(c => c.Numeric));
    }

    public override void DrawSettings()
    {
        base.DrawSettings();
        if (ImGui.Button("Open GemLens2")) _open = true;
        Wrapped("Press the open key while hovering an inventory gem, or open the window and search. Drag the title bar to move; drag the corner to resize.");
        if (_error.Length > 0) Wrapped(_error);
    }

    public override void Render()
    {
        if (!Settings.Enable.Value || !_open || !GameController.InGame) return;
        float scale = Settings.Scale.Value / 100f;
        ImGui.SetNextWindowSize(new Vector2(940,620)*scale, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(740,440)*scale, new Vector2(float.MaxValue));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.055f,.065f,.08f,.98f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.10f,.29f,.28f,1));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(.14f,.36f,.34f,1));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.12f,.23f,.25f,1));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14,12));
        bool visible = ImGui.Begin("GemLens2###GemLens2Window", ref _open, ImGuiWindowFlags.NoCollapse);
        try
        {
            ImGui.SetWindowFontScale(scale);
            if (!visible) return;
            if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && ImGui.IsKeyPressed(ImGuiKey.Escape))
            { _open = false; return; }
            if (_error.Length > 0) { Wrapped(_error); return; }
            ImGui.BeginChild("gem-list",new Vector2(205*scale,0),ImGuiChildFlags.Border);
            try { DrawList(); } finally { ImGui.EndChild(); }
            ImGui.SameLine();
            ImGui.BeginChild("gem-details",Vector2.Zero,ImGuiChildFlags.None);
            try { DrawDetails(); } finally { ImGui.EndChild(); }
        }
        finally { ImGui.End(); ImGui.PopStyleVar(); ImGui.PopStyleColor(4); }
    }

    private void DrawList()
    {
        Colored(Teal,"SKILL REFERENCE");
        if (ImGui.BeginTabBar("skill-sources"))
        {
            if (ImGui.BeginTabItem("My skills")) { DrawMySkills(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("All skills")) { DrawCatalog(); ImGui.EndTabItem(); }
            ImGui.EndTabBar();
        }
    }

    private void DrawCatalog()
    {
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##search","Search gems...",ref _search,120)) Filter();
        if (ImGui.Checkbox("Favorites only",ref _favoritesOnly)) Filter();
        Colored(Muted,$"{_filtered.Length} skills");
        ImGui.Separator();
        ImGui.BeginChild("results",new Vector2(0,-38),ImGuiChildFlags.None);
        try
        {
            foreach (var gem in _filtered)
            {
                if (ImGui.Selectable(gem.Name+"##"+gem.Slug,gem == _gem)) { Select(gem); _notice = ""; }
                if (ImGui.IsItemHovered()) Tooltip(gem.Name + (gem.Category.Length>0 ? "\n"+gem.Category : ""));
            }
            if (_filtered.Length == 0) Wrapped("No matches. Try a shorter name or turn off Favorites only.");
        }
        finally { ImGui.EndChild(); }
    }

    private void DrawDetails()
    {
        if (_gem == null) return;
        Colored(Teal,_gem.Name);
        bool favorite = Settings.Favorites.Contains(_gem.Slug);
        ImGui.SameLine();
        if (ImGui.SmallButton(favorite ? "Unfavorite" : "Favorite"))
        { if (favorite) Settings.Favorites.Remove(_gem.Slug); else Settings.Favorites.Add(_gem.Slug); Filter(); }
        Colored(Muted, string.Join("  /  ",new[]{_gem.Category,_gem.Weapon}.Where(s=>s.Length>0)));
        if (_notice.Length > 0) Wrapped(_notice);
        DrawDetectedLevels();
        Colored(Muted,"Base skill values, before your build's modifiers.");
        ImGui.Separator();
        if (_gem.Tables.Count > 1)
        {
            ImGui.SetNextItemWidth(220);
            if (ImGui.BeginCombo("Progression",$"Table {_table+1}"))
            {
                for(int i=0;i<_gem.Tables.Count;i++)
                    if(ImGui.Selectable($"Table {i+1}",i==_table)){_table=i;ResetLevels(20);}
                ImGui.EndCombo();
            }
        }
        if (Current is { } t)
        {
            LevelPicker("From",ref _from,t); ImGui.SameLine(); LevelPicker("To",ref _to,t);
            ImGui.SameLine(); ImGui.BeginDisabled(_to == 0);
            if(ImGui.SmallButton("-1")) StepComparison(-1);
            ImGui.EndDisabled(); ImGui.SameLine(); ImGui.BeginDisabled(_to == t.Levels.Length-1);
            if(ImGui.SmallButton("+1")) StepComparison(1);
            ImGui.EndDisabled();
            ImGui.SameLine(); if(ImGui.SmallButton("Swap")) (_from,_to)=(_to,_from);
            if (ImGui.BeginTabBar("views"))
            {
                if (ImGui.BeginTabItem("Graph")) { DrawGraph(t); DrawComparison(t); ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("Table")) { DrawTable(t); ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("Quality")) { DrawQuality(); ImGui.EndTabItem(); }
                ImGui.EndTabBar();
            }
        }
        else
        {
            Wrapped("No level progression table is published for this entry in the bundled snapshot.");
            DrawQuality();
        }
        ImGui.Spacing(); ImGui.Separator();
        Colored(Muted,"Patch 0.5.5  |  Database updated: " + _database.ImportedUtc.Split('T')[0]);
    }

    private static void LevelPicker(string label,ref int row,Progression t)
    {
        ImGui.SetNextItemWidth(95);
        if(ImGui.BeginCombo(label,t.Levels[row].ToString()))
        {
            for(int i=0;i<t.Levels.Length;i++) if(ImGui.Selectable(t.Levels[i].ToString(),i==row))row=i;
            ImGui.EndCombo();
        }
    }

    private void DrawQuality()
    {
        ImGui.Spacing();
        Colored(Teal,"Quality effects");
        Wrapped("Reference ranges from the source. Alternate quality requires Advanced Thaumaturgy; it is not applied to the level comparisons.");
        ImGui.Spacing();
        if(_gem!.Quality.Length==0) Wrapped("No quality effect is listed in this snapshot.");
        foreach(string line in _gem.Quality) { ImGui.Bullet(); ImGui.SameLine(); Wrapped(line); ImGui.Spacing(); }
    }

    private void DrawComparison(Progression t)
    {
        if(!ImGui.BeginTable("comparison",4,ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH|ImGuiTableFlags.SizingStretchProp))return;
        ImGui.TableSetupColumn("Stat",ImGuiTableColumnFlags.None,1.5f);
        ImGui.TableSetupColumn($"Level {t.Levels[_from]}");ImGui.TableSetupColumn($"Level {t.Levels[_to]}");ImGui.TableSetupColumn("Change");ImGui.TableHeadersRow();
        foreach(var c in t.Columns)
        {
            if(c.Name.Equals("Req",StringComparison.OrdinalIgnoreCase))continue;
            ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.TextUnformatted(c.Name);
            ImGui.TableNextColumn();Colored(Teal,c.Cells[_from]);
            ImGui.TableNextColumn();Colored(Gold,c.Cells[_to]);
            ImGui.TableNextColumn();ImGui.TextUnformatted(Plain(Comparison.Delta(c.Value(_from,0),c.Value(_to,0))));
        }
        ImGui.EndTable();
        Colored(Muted,"Range changes use averages. Percent stats show point changes.");
    }

    private void DrawTable(Progression t)
    {
        Wrapped("Requirements are the source's base-gem requirements, not requirements imposed by +gem-level equipment.");
        if(!ImGui.BeginTable("levels",t.Columns.Count+1,ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH|ImGuiTableFlags.ScrollX|ImGuiTableFlags.ScrollY,
               new Vector2(0,Math.Max(180,ImGui.GetContentRegionAvail().Y-65))))return;
        ImGui.TableSetupScrollFreeze(1,1);ImGui.TableSetupColumn("Level",ImGuiTableColumnFlags.WidthFixed,50);
        foreach(var c in t.Columns)ImGui.TableSetupColumn(c.Name,ImGuiTableColumnFlags.WidthFixed,
            Math.Max(c.Name=="Req"?175:110,ImGui.CalcTextSize(c.Name).X+20));
        ImGui.TableHeadersRow();
        for(int i=0;i<t.Levels.Length;i++)
        {
            ImGui.TableNextRow();ImGui.TableNextColumn();
            if(i==_from||i==_to)ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0,ImGui.ColorConvertFloat4ToU32(new Vector4(.1f,.25f,.25f,.8f)));
            if(ImGui.Selectable(t.Levels[i]+"##level"+i,i==_to))_to=i;
            if (_scrollToLevel && i == _from) { ImGui.SetScrollHereY(.3f); _scrollToLevel = false; }
            if(ImGui.IsItemHovered())Tooltip("Click to choose comparison level");
            foreach(var c in t.Columns){ImGui.TableNextColumn();ImGui.TextUnformatted(Plain(c.Cells[i]));}
        }
        ImGui.EndTable();
    }

    private void DrawGraph(Progression t)
    {
        var stats=t.Columns.Select((c,i)=>(c,i)).Where(p=>p.c.Numeric).ToArray();
        if(stats.Length==0){Wrapped("No numeric series available.");return;}
        if(!t.Columns[_stat].Numeric)_stat=stats[0].i;
        ImGui.SetNextItemWidth(190);
        if(ImGui.BeginCombo("##stat",t.Columns[_stat].Name))
        {
            foreach(var p in stats)if(ImGui.Selectable(p.c.Name,p.i==_stat))_stat=p.i;
            ImGui.EndCombo();
        }
        if(t.Columns[_stat].HasRange)
        {
            ImGui.SameLine();ImGui.SetNextItemWidth(110);
            string[] modes={"Average","Minimum","Maximum"};
            if(ImGui.BeginCombo("##range",modes[_rangeMode]))
            {for(int i=0;i<modes.Length;i++)if(ImGui.Selectable(modes[i],i==_rangeMode))_rangeMode=i;ImGui.EndCombo();}
        }
        ImGui.SameLine();ImGui.Checkbox("All levels",ref _fullRange);
        int lo=_fullRange?0:Math.Max(0,Math.Min(_from,_to)-4), hi=_fullRange?t.Levels.Length-1:Math.Min(t.Levels.Length-1,Math.Max(_from,_to)+4);
        var c=t.Columns[_stat];
        var values=Enumerable.Range(lo,hi-lo+1).Select(i=>c.Value(i,_rangeMode)).Where(v=>v.HasValue).Select(v=>v!.Value).ToArray();
        if(values.Length==0){Wrapped("No numeric values in this level range.");return;}
        double min=Math.Min(0,values.Min()),max=values.Max();if(max<=min)max=min+1;
        max+=(max-min)*.1;
        var origin=ImGui.GetCursorScreenPos();var size=new Vector2(Math.Max(220,ImGui.GetContentRegionAvail().X),205*Settings.Scale.Value/100f);
        ImGui.InvisibleButton("chart",size);
        var draw=ImGui.GetWindowDrawList();
        var a=origin+new Vector2(58,14);var b=origin+size-new Vector2(16,32);
        uint grid=ImGui.ColorConvertFloat4ToU32(new Vector4(.20f,.24f,.28f,.7f));uint muted=ImGui.ColorConvertFloat4ToU32(Muted);
        float X(int i)=>a.X+(b.X-a.X)*(t.Levels[i]-t.Levels[lo])/Math.Max(1,t.Levels[hi]-t.Levels[lo]);
        float Y(double v)=>b.Y-(float)((v-min)/(max-min))*(b.Y-a.Y);
        for(int i=0;i<=4;i++)
        {double v=min+(max-min)*i/4;float y=Y(v);draw.AddLine(new Vector2(a.X,y),new Vector2(b.X,y),grid);draw.AddText(new Vector2(origin.X,y-7),muted,Comparison.Number(v));}
        for(int i=lo;i<=hi;i++)
        {
            var value=c.Value(i,_rangeMode);if(!value.HasValue)continue;
            var point=new Vector2(X(i),Y(value.Value));
            if(i>lo && c.Value(i-1,_rangeMode) is double previous)draw.AddLine(new Vector2(X(i-1),Y(previous)),point,ImGui.ColorConvertFloat4ToU32(Teal),2);
            if(i==_from||i==_to)
            {uint color=ImGui.ColorConvertFloat4ToU32(i==_to?Gold:Teal);draw.AddLine(new Vector2(point.X,a.Y),new Vector2(point.X,b.Y),color);draw.AddCircleFilled(point,4,color);}
            if(i==lo||i==hi||i==_from||i==_to)draw.AddText(new Vector2(point.X-5,b.Y+5),muted,t.Levels[i].ToString());
        }
        if(ImGui.IsItemHovered())
        {
            float x=ImGui.GetMousePos().X;
            int nearest=Enumerable.Range(lo,hi-lo+1).MinBy(i=>Math.Abs(X(i)-x));
            Tooltip($"Level {t.Levels[nearest]}\n{c.Name}: {c.Cells[nearest]}\nClick to compare this level");
            if(ImGui.IsMouseClicked(ImGuiMouseButton.Left))_to=nearest;
        }
        Colored(Teal,$"From: {t.Levels[_from]}");ImGui.SameLine();Colored(Gold,$"To: {t.Levels[_to]}");
    }
}
