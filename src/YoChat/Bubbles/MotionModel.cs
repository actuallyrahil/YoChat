record Obstacle(double Left,double Top,double Right,double Bottom);

static class PlacementModel {
 public static double Clearance(PillPosition pill,IEnumerable<Obstacle> obstacles,double normalGap=28){
  double gap=normalGap;
  foreach(var r in obstacles){
   if(r.Bottom<=pill.Top+2&&r.Top>=pill.Top-760&&r.Right>pill.CenterX-160&&r.Left<pill.CenterX+160&&r.Right-r.Left>=8&&r.Bottom-r.Top>=8)
    gap=Math.Max(gap,pill.Top-r.Top+16);
  }
  return gap;
 }
}

sealed class DragFollower {
 bool wasDown,armed,dragging;double mouseX,mouseY,anchorX,anchorY;long released;
 PillPosition? predicted;
 public PillPosition Update(PillPosition observed,bool down,double x,double y,long now){
  if(!observed.Visible){wasDown=down;armed=dragging=false;predicted=null;return observed;}
  if(down&&!wasDown){armed=Math.Abs(x-observed.CenterX)<140&&y>=observed.Top-3&&y<=observed.Top+48;dragging=false;mouseX=x;mouseY=y;anchorX=observed.CenterX;anchorY=observed.Top;predicted=null;}
  if(down&&armed){
   if(Math.Abs(x-mouseX)+Math.Abs(y-mouseY)>5)dragging=true;
   if(dragging)predicted=new(true,anchorX+x-mouseX,anchorY+y-mouseY);
  }
  if(!down&&wasDown&&dragging){released=now;armed=dragging=false;}
  wasDown=down;
  if(predicted!=null){
   if(down&&armed)return predicted;
   if(now-released<100&&Math.Abs(observed.CenterX-predicted.CenterX)+Math.Abs(observed.Top-predicted.Top)>3)return predicted;
   predicted=null;
  }
  return observed;
 }
}

static class MotionTests {
 public static void Run(){
  var p=new PillPosition(true,500,600);
  if(PlacementModel.Clearance(p,[])!=28)throw new Exception("Default clearance");
  if(PlacementModel.Clearance(p,[new(340,480,660,580)])!=136)throw new Exception("Prompt clearance");
  if(PlacementModel.Clearance(p,[new(900,480,1100,580)])!=28)throw new Exception("Unrelated prompt must not move bubbles");
  if(PlacementModel.Clearance(p,[new(0,0,2560,1440)])!=28)throw new Exception("Root container must not count as prompt");
  var d=new DragFollower();d.Update(p,true,500,620,0);var moved=d.Update(p,true,650,500,16);
  if(moved.CenterX!=650||moved.Top!=480)throw new Exception("Drag must follow both axes before UIA catches up");
  if(d.Update(p,false,650,500,32)!=moved)throw new Exception("Release must bridge short UIA lag");
  if(d.Update(p,false,650,500,150)!=p)throw new Exception("Prediction must expire");
  var outside=new DragFollower();outside.Update(p,true,100,100,0);if(outside.Update(p,true,300,300,16)!=p)throw new Exception("Outside drag must be ignored");
  if(d.Update(new(false,0,0),true,650,500,200).Visible)throw new Exception("Closed voice must hide immediately");
  Console.WriteLine("PASS: prompt avoidance, default spacing, unrelated-window rejection, two-axis drag, release recovery, closed voice");
 }
}

