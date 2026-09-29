using System.Globalization;
using System.Windows.Controls;
using System.Windows.Media;

namespace PatarikAIOS;

public static partial class Views
{
    private static UIElement ShowcaseMetric(string title, string value, string hint, string symbol, string color, Action? action = null)
    {
        var body = new Grid { MinHeight = 82 };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        var icon = new Border { Width = 40, Height = 44, CornerRadius = new CornerRadius(12), Background = BrushFor(color + ""), Opacity = 0.95, VerticalAlignment = VerticalAlignment.Top,
            Child = Text(symbol, 24, FontWeights.SemiBold, Brushes.White) };
        ((TextBlock)icon.Child).HorizontalAlignment = HorizontalAlignment.Center;
        ((TextBlock)icon.Child).VerticalAlignment = VerticalAlignment.Center;
        body.Children.Add(icon);
        var labels = new StackPanel(); labels.Children.Add(Text(title, 13, FontWeights.SemiBold, BrushFor("#596E8D")));
        labels.Children.Add(new Border { Margin = new Thickness(0,8,0,7), Child = Text(value, value.Length > 17 ? 18 : 25, FontWeights.Bold, BrushFor(title == "Շահույթ" ? "#07865D" : "#10213D")) });
        labels.Children.Add(Text(hint, 11, null, BrushFor("#7A8BA5"))); Grid.SetColumn(labels, 1); body.Children.Add(labels);
        var card = Card(body); card.Padding = new Thickness(16);
        if (action is null) return card;
        var button = new Button { Content = card, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Margin = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) => action(); return button;
    }

    private static Grid TileRow(params UIElement[] items)
    {
        var grid = new Grid { Margin = new Thickness(0,10,0,0) };
        foreach(var item in items) grid.Children.Add(item);
        void ArrangeColumns(int columns)
        {
            if(grid.ColumnDefinitions.Count == columns) return;
            grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear();
            for(var c=0;c<columns;c++) grid.ColumnDefinitions.Add(new ColumnDefinition());
            for(var r=0;r<(items.Length+columns-1)/columns;r++) grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            for(var i=0;i<items.Length;i++) { Grid.SetColumn(items[i],i%columns); Grid.SetRow(items[i],i/columns); }
        }
        ArrangeColumns(Math.Max(1,items.Length));
        grid.SizeChanged += (_,_) => ArrangeColumns(items.Length>=4 && grid.ActualWidth<850 ? 2 : Math.Max(1,items.Length));
        return grid;
    }

    private static Grid Split(UIElement left, UIElement right, double ratio = 1)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ratio, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(left); Grid.SetColumn(right, 1); grid.Children.Add(right);
        bool stacked=false;
        grid.SizeChanged += (_,_) =>
        {
            var small=grid.ActualWidth<850; if(small==stacked) return; stacked=small;
            grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear();
            if(small) { grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto}); grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto}); Grid.SetColumn(right,0); Grid.SetRow(right,1); }
            else { grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(ratio,GridUnitType.Star)}); grid.ColumnDefinitions.Add(new ColumnDefinition()); Grid.SetColumn(right,1); Grid.SetRow(right,0); }
        };
        return grid;
    }

    private static void RowSurface(Grid grid, int row)
    {
        var band = new Border { Background = row % 2 == 0 ? BrushFor("#FAFCFF") : Brushes.White, BorderBrush = BrushFor("#E5ECF5"), BorderThickness = new Thickness(0,0,0,1), IsHitTestVisible = false };
        Grid.SetRow(band,row); Grid.SetColumnSpan(band, Math.Max(1,grid.ColumnDefinitions.Count)); grid.Children.Add(band);
    }

    private static UIElement WarningBanner(string warning)
    {
        var lines = warning.Split('\n',StringSplitOptions.RemoveEmptyEntries);
        return new Border { Background = BrushFor("#FFF6E5"), BorderBrush = BrushFor("#F5D99D"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12,8,12,8), Margin = new Thickness(0,0,12,8),
            Child = new Expander { Header = "⚠ Տվյալների զգուշացումներ · բացել մանրամասները", Foreground = BrushFor("#98670C"),
                Content = Text(string.Join("\n",lines),12,null,BrushFor("#98670C")) } };
    }

    private static UIElement AdaptiveOrders(UIElement top, UIElement body, UIElement bottom)
    {
        var host = new Grid();
        var desktop = new DockPanel();
        var stacked = new StackPanel();
        var table = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        var compactScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = stacked };
        bool? compactMode = null;
        void Reflow(bool compact)
        {
            if (compactMode == compact) return;
            compactMode = compact;
            host.Children.Clear(); desktop.Children.Clear(); stacked.Children.Clear(); table.Content = null;
            table.Content = body;
            table.VerticalScrollBarVisibility = compact ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
            if (compact)
            {
                stacked.Children.Add(top); stacked.Children.Add(table); stacked.Children.Add(bottom); host.Children.Add(compactScroll);
            }
            else
            {
                DockPanel.SetDock(top,Dock.Top); desktop.Children.Add(top);
                DockPanel.SetDock(bottom,Dock.Bottom); desktop.Children.Add(bottom);
                desktop.Children.Add(table); host.Children.Add(desktop);
            }
        }
        Reflow(false);
        host.SizeChanged += (_,_) => Reflow(host.ActualHeight < 650 || host.ActualWidth < 950);
        return host;
    }
}

internal static class PresentationTheme
{
    internal static Brush Blue => new SolidColorBrush(Color.FromRgb(9,98,255));
    internal static void Apply(DependencyObject root)
    {
        if (root is Button b && b.Background is SolidColorBrush brush && (brush.Color == Color.FromRgb(15,118,110) || brush.Color == Color.FromRgb(22,101,52))) b.Background = Blue;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) Apply(child);
    }
}

// These visuals only display supplied values; they never save or recompute business records.
internal abstract class ReportVisual : FrameworkElement
{
    protected static Brush B(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    protected void Label(DrawingContext dc, string value, Point at, double size = 11, string color = "#72839C", bool bold = false)
    {
        var text = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, B(color), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(text, at);
    }
}

internal sealed class SalesTrendVisual(IReadOnlyList<(DateOnly Date, decimal? Amount)> points) : ReportVisual
{
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var w = Math.Max(160, ActualWidth - 90); var h = Math.Max(60,ActualHeight - 48);
        var values = points.Where(p => p.Amount.HasValue).Select(p => p.Amount!.Value).ToList();
        if (values.Count == 0) { Label(dc,"Վաճառքի տվյալներ չկան",new Point(20,80),16); return; }
        var max = Math.Max(1m, values.Max() * 1.15m); var min = Math.Min(0m,values.Min() * 1.15m);
        Point At(int i) => new(66 + i * w / Math.Max(1, points.Count-1), 8 + h * (double)((max-points[i].Amount!.Value)/(max-min)));
        for (int i=0;i<=4;i++) { var y=8+h*i/4; dc.DrawLine(new Pen(B("#E8EEF7"),1),new Point(66,y),new Point(66+w,y)); Label(dc,(max-(max-min)*i/4).ToString("N0"),new Point(0,y-7)); }
        for(int i=0;i<points.Count;i++)
        {
            var x=66+i*w/Math.Max(1,points.Count-1); Label(dc,points[i].Date.ToString("dd.MM"),new Point(x-15,h+20));
            if (!points[i].Amount.HasValue) continue;
            var p=At(i);
            if (i>0 && points[i-1].Amount.HasValue)
            {
                var previous=At(i-1); var area=new StreamGeometry(); using(var c=area.Open()) { c.BeginFigure(previous,true,true); c.LineTo(p,true,false); c.LineTo(new Point(p.X,8+h),true,false); c.LineTo(new Point(previous.X,8+h),true,false); }
                dc.DrawGeometry(B("#E6F0FF"),null,area); dc.DrawLine(new Pen(B("#0962FF"),2.5),previous,p);
            }
            dc.DrawEllipse(Brushes.White,new Pen(B("#0962FF"),2),p,3.5,3.5);
            Label(dc,points[i].Amount!.Value.ToString("N0"),new Point(p.X-22,Math.Max(0,p.Y-23)),10,"#243B63",true);
        }
    }
}

internal sealed class FundsRingVisual(AvailableFundsBreakdown funds) : ReportVisual
{
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var radius=Math.Min(69,ActualWidth*0.2); var center=new Point(radius+15,104);
        var values=new[] { ("Կանխիկ",funds.Cash,"#17BA8E"), ("Բանկ / POS",funds.BankReport+funds.AmeriabankPos099,"#0962FF"), ("Idram",funds.Idram,"#FB923C") };
        dc.DrawEllipse(null,new Pen(B("#E8EEF7"),22),center,radius,radius);
        var total=values.Sum(x=>x.Item2); var angle=-90d;
        if(values.All(x=>x.Item2>=0) && total>0)
        foreach(var item in values.Where(x=>x.Item2>0))
        {
            var sweep=(double)(item.Item2/total)*359.99;
            Point P(double a)=>new(center.X+radius*Math.Cos(a*Math.PI/180),center.Y+radius*Math.Sin(a*Math.PI/180));
            var arc=new StreamGeometry(); using(var c=arc.Open()) { c.BeginFigure(P(angle),false,false); c.ArcTo(P(angle+sweep),new Size(radius,radius),0,sweep>180,SweepDirection.Clockwise,true,false); }
            dc.DrawGeometry(null,new Pen(B(item.Item3),22),arc); angle+=sweep;
        }
        Label(dc,total.ToString("N0"),new Point(center.X-radius+10,center.Y-14),16,"#10213D",true);
        Label(dc,"դրամ",new Point(center.X-16,center.Y+11));
        var left=radius*2+42;
        for(var i=0;i<values.Length;i++) { var y=38+i*58; dc.DrawEllipse(B(values[i].Item3),null,new Point(left,y+7),4,4); Label(dc,values[i].Item1,new Point(left+12,y),12,"#405574"); Label(dc,values[i].Item2.ToString("N0")+" ֏",new Point(left+12,y+20),15,"#10213D",true); }
    }
}
