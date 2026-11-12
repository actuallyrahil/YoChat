using System.Text.Json;

sealed class ContinuityReader {
 Dictionary<string,VoiceRow[]>? previous;
 public string? ThreadId{get;private set;}
 public VoiceRow[] Rows{get;private set;}=[];
 public void Reset(){ThreadId=null;Rows=[];previous=null;}
 public void Update(string json){
  using var document=JsonDocument.Parse(json);
  if(document.RootElement.GetProperty("version").GetInt32()!=1)throw new JsonException("Unsupported voice continuity format");
  var next=new Dictionary<string,VoiceRow[]>();
  foreach(var thread in document.RootElement.GetProperty("threads").EnumerateObject()){
   int index=0;var items=new List<VoiceRow>();
   foreach(var item in thread.Value.GetProperty("items").EnumerateArray()){
    var role=item.GetProperty("role").GetString();var text=item.GetProperty("text").GetString();
    if(role is "user" or "assistant"&& !string.IsNullOrWhiteSpace(text))items.Add(new(role,text,thread.Name+":"+index++));
   }
   next[thread.Name]=items.TakeLast(5).ToArray();
  }
  if(previous!=null){
   var changed=next.Where(pair=>!previous.TryGetValue(pair.Key,out var old)||!pair.Value.SequenceEqual(old)).ToArray();
   if(changed.Length>0){var selected=changed.Last();ThreadId=selected.Key;Rows=selected.Value;}
  }
  previous=next;
 }
 public static void Test(){
  string Snapshot(string thread,string text)=>JsonSerializer.Serialize(new{version=1,threads=new Dictionary<string,object>{{thread,new{items=new[]{new{role="user",text},new{role="assistant",text="Reply"}}}}}});
  var reader=new ContinuityReader();reader.Update(Snapshot("old","Previous call"));
  if(reader.Rows.Length!=0)throw new Exception("Must not show an old call at startup");
  reader.Update(Snapshot("old","New spoken sentence"));
  if(reader.Rows.Length!=2||reader.Rows[0].Text!="New spoken sentence")throw new Exception("Live completed segments must update without a window");
  reader.Reset();reader.Update(Snapshot("old","New spoken sentence"));reader.Update(Snapshot("next","Second call"));
  if(reader.ThreadId!="next"||reader.Rows[0].Text!="Second call")throw new Exception("Next wake must follow the new call");
  var before=reader.Rows;try{reader.Update("{\"threads\":");}catch(JsonException){}
  if(!reader.Rows.SequenceEqual(before))throw new Exception("A partial file write must preserve the last complete snapshot");
  Console.WriteLine("PASS: direct transcript updates, no old-call startup flash, new-call tracking and partial-write recovery");
 }
}

