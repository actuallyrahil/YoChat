using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using C=System.Windows.Controls;
using Brushes=System.Windows.Media.Brushes;
using FontFamily=System.Windows.Media.FontFamily;
using Size=System.Windows.Size;
using Color=System.Windows.Media.Color;
using ColorConverter=System.Windows.Media.ColorConverter;

record ViewSnapshot(PillPosition Pill,VoiceRow[] Rows,double Clearance,long PositionAt,long TranscriptAt);

sealed class BubbleView : Window {
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")] static extern bool GetCursorPos(out CursorPoint p);
 [StructLayout(LayoutKind.Sequential)] struct CursorPoint{public int X,Y;}
 readonly C.StackPanel stack=new(){Margin=new Thickness(10,8,10,8)};
 readonly Func<ViewSnapshot> read;
 readonly DragFollower drag=new();
 VoiceRow[] previous=[];double budget=-1;IntPtr handle;bool closing;
 int lastX=int.MinValue,lastY=int.MinValue;
 readonly System.Windows.Threading.DispatcherTimer timer=new(System.Windows.Threading.DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(16)};
 public int DrawnRows{get;private set;}
 public int MoveFrames{get;private set;}
 internal int FrameTicks{get;private set;}
 public int LastX=>lastX;public int LastY=>lastY;
 public double LastClearance{get;private set;}
 public BubbleView(Func<ViewSnapshot> read,Action exit){
  this.read=read;Title="YoChat Bubbles";Width=320;SizeToContent=SizeToContent.Height;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;ShowActivated=false;FontFamily=new FontFamily("Segoe UI Variable Text, Segoe UI");FontWeight=FontWeights.Normal;Content=stack;
  var menu=new C.ContextMenu();var item=new C.MenuItem{Header="Exit YoChat"};item.Click+=(_,_)=>exit();menu.Items.Add(item);ContextMenu=menu;
  SourceInitialized+=(_,_)=>handle=new WindowInteropHelper(this).Handle;
  timer.Tick+=OnFrame;timer.Start();
  Closed+=(_,_)=>{closing=true;timer.Stop();};
 }
 void OnFrame(object? sender,EventArgs args){
  if(closing)return;
  FrameTicks++;
  var state=read();long now=Environment.TickCount64;
  if(!state.Pill.Visible||now-state.PositionAt>500||now-state.TranscriptAt>3000||state.Rows.Length==0){if(IsVisible)Hide();return;}
  GetCursorPos(out var cursor);var p=drag.Update(state.Pill,(GetAsyncKeyState(1)&0x8000)!=0,cursor.X,cursor.Y,now);
  var device=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice??Matrix.Identity;
  double sx=device.M11,sy=device.M22;
  double clearance=Math.Max(28*sy,state.Clearance);LastClearance=clearance;
  var screen=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)p.CenterX,(int)p.Top)).WorkingArea;
  double available=Math.Min(480,(p.Top-clearance-screen.Top)/sy-8);
  if(available<61){if(IsVisible)Hide();return;}
  if(!state.Rows.SequenceEqual(previous)||Math.Abs(available-budget)>1){RenderRows(state.Rows,available);previous=state.Rows;budget=available;}
  int x=(int)Math.Round(p.CenterX-Width*sx/2),y=(int)Math.Round(p.Top-clearance-stack.DesiredSize.Height*sy);
  if(!IsVisible)Show();
  if(x!=lastX||y!=lastY){SetWindowPos(handle,IntPtr.Zero,x,y,0,0,0x0001|0x0004|0x0010);lastX=x;lastY=y;MoveFrames++;}
 }
 internal void RenderRows(VoiceRow[] rows,double maxHeight){
  var bubbles=new List<C.Border>();
  foreach(var row in rows.TakeLast(5)){
   var b=Bubble(row,false);b.Measure(new Size(300,double.PositiveInfinity));
   if(b.DesiredSize.Height>maxHeight-16){b=Bubble(row,true);b.Measure(new Size(300,double.PositiveInfinity));}
   bubbles.Add(b);
  }
  double total=16+bubbles.Sum(b=>b.DesiredSize.Height);
  while(total>maxHeight&&bubbles.Count>1){total-=bubbles[0].DesiredSize.Height;bubbles.RemoveAt(0);}
  stack.Children.Clear();foreach(var b in bubbles)stack.Children.Add(b);DrawnRows=bubbles.Count;
  stack.Measure(new Size(320,double.PositiveInfinity));stack.Arrange(new Rect(0,0,320,stack.DesiredSize.Height));UpdateLayout();
 }
 static C.Border Bubble(VoiceRow row,bool full){
  bool user=row.Speaker=="user";
  var b=new C.Border{MaxWidth=278,Padding=new Thickness(14,8,14,8),Margin=new Thickness(0,0,0,6),HorizontalAlignment=user?System.Windows.HorizontalAlignment.Right:System.Windows.HorizontalAlignment.Left,CornerRadius=new CornerRadius(19),Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(user?"#4A79EB":"#303035")),UseLayoutRounding=true,SnapsToDevicePixels=true};
  if(full){var button=new C.Button{Content="Read full message",FontSize=14,Foreground=Brushes.White,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0)};button.Click+=(_,_)=>ShowFull(row.Text);b.Child=button;}
  else b.Child=new C.TextBlock{Text=row.Text,FontSize=15,LineHeight=21,LineStackingStrategy=LineStackingStrategy.BlockLineHeight,TextAlignment=TextAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap};
  return b;
 }
 static void ShowFull(string text){var w=new Window{Title="Full voice message",Width=520,Height=600,Topmost=true,Content=new C.TextBox{Text=text,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=C.ScrollBarVisibility.Auto,FontSize=15,Padding=new Thickness(20)}};w.Show();}
 internal static void Test(){
  var v=new BubbleView(()=>new(new(false,0,0),[],28,0,0),()=>{});
  v.RenderRows([new("user","A short question","1"),new("assistant",string.Concat(Enumerable.Repeat("A longer complete reply. ",25)),"2")],200);
  if(v.DrawnRows<1||v.stack.DesiredSize.Height>200)throw new Exception("Complete bubbles exceeded available height");
  v.RenderRows([new("assistant",new string('x',5000),"3")],100);
  if(v.DrawnRows!=1||((C.Border)v.stack.Children[0]).Child is not C.Button)throw new Exception("Oversized message disclosure missing");
  using var context=new System.Windows.Forms.ApplicationContext();using var stop=new System.Windows.Forms.Timer{Interval=150};stop.Tick+=(_,_)=>context.ExitThread();stop.Start();System.Windows.Forms.Application.Run(context);stop.Stop();
  if(v.FrameTicks<2)throw new Exception("Hidden viewer did not receive rendering ticks");
  v.Close();Console.WriteLine("PASS: compiled layout, oversized disclosure, and hidden-viewer rendering loop");
 }
}


