using System.Text.RegularExpressions;

record VoiceRow(string Speaker,string Text,string Timestamp);
record UiText(string Name,string Type);
record PillPosition(bool Visible,double CenterX,double Top);

static class LiveModel {
 public static (double Left,double Bottom) Place(PillPosition p,double width,double gap=12)=> (p.CenterX-width/2,p.Top-gap);
 public static VoiceRow[] Parse(IEnumerable<UiText> nodes) {
  var rows=new List<VoiceRow>();string? role=null;var parts=new List<string>();int index=0;
  void Flush(){if(role!=null&&parts.Count>0)rows.Add(new(role,string.Join(" ",parts),"live-"+index++));parts.Clear();}
  foreach(var node in nodes) {
   var s=node.Name.Trim();
   if(s=="Latest response"){Flush();role=null;break;}
   if(s is "You said:" or "ChatGPT said:"){Flush();role=s=="You said:"?"user":"assistant";continue;}
   if(node.Type=="ControlType.Button" && s is "Copy" or "Copy message"){Flush();role=null;continue;}
   if(role!=null&&node.Type=="ControlType.Text"&&s.Length>0&&!Regex.IsMatch(s,@"^\d{1,2}:\d{2}\s*[AP]M$"))parts.Add(s);
  }
  Flush();return rows.TakeLast(5).ToArray();
 }
 public static void Test() {
  var a=Place(new(true,126,487),320);if(a!=(-34d,475d))throw new Exception("Pill centring failed");
  var b=Place(new(true,-174,287),320);if(b!=(-334d,275d))throw new Exception("Two-axis movement failed");
  UiText[] first=[new("You said:","ControlType.Text"),new("Hello","ControlType.Text"),new("Copy message","ControlType.Button"),new("ChatGPT said:","ControlType.Text"),new("Hi","ControlType.Text")];
  var rows=Parse(first);if(rows.Length!=2||rows[0].Speaker!="user"||rows[1].Text!="Hi")throw new Exception("Role parsing failed");
  var longer=Parse(first[..^1].Append(new("Hi there","ControlType.Text")));if(longer.Length!=2||longer[1].Timestamp!=rows[1].Timestamp||longer[1].Text!="Hi there")throw new Exception("Streaming replacement failed");
  if(Parse([new("Unrelated text","ControlType.Text")]).Length!=0)throw new Exception("Unscoped text accepted");
  var composer=Parse(first.Concat([new("Latest response","ControlType.Text"),new("Work with ChatGPT","ControlType.Text")]));if(composer[^1].Text!="Hi")throw new Exception("Composer text leaked into transcript");
  Console.WriteLine("PASS: pill centring, two-axis motion, speaker roles, streaming replacement, unscoped text rejection");
 }
}

