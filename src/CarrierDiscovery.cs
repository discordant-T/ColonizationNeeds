using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// Read-only discovery: journal history supplies identity, never stock or delivery credit.
public static class CarrierDiscovery
{
    static string Text(Dictionary<string,object> entry,string key) { object value; return entry.TryGetValue(key,out value)?Convert.ToString(value):""; }
    public static List<CarrierInventory.Carrier> Scan(string folder,string expectedCommander)
    {
        if(!Directory.Exists(folder)) throw new IOException("Journal folder not found. Choose it in Settings, then dock at the carrier and rescan.");
        var found=new Dictionary<string,CarrierInventory.Carrier>(); var json=new JavaScriptSerializer(); string commander="";
        foreach(var file in Directory.GetFiles(folder,"Journal.*.log").OrderBy(x=>Path.GetFileName(x),StringComparer.OrdinalIgnoreCase))
        {
            using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(var reader=new StreamReader(stream))
            {
                string line;
                while((line=reader.ReadLine())!=null)
                {
                    Dictionary<string,object> entry;
                    try { entry=json.DeserializeObject(line.TrimStart('\uFEFF')) as Dictionary<string,object>; } catch(ArgumentException) { continue; }
                    if(entry==null) continue;
                    string evt=Text(entry,"event");
                    if(evt=="LoadGame") commander=Text(entry,"Commander");
                    if(evt=="Commander") commander=Text(entry,"Name");
                    if(evt=="Shutdown") { commander=""; continue; }
                    if(!String.Equals(commander,expectedCommander.Trim(),StringComparison.OrdinalIgnoreCase)) continue;
                    string id="", name="", callsign="";
                    if(evt=="CarrierStats") { id=Text(entry,"CarrierID");name=Text(entry,"Name");callsign=Text(entry,"Callsign"); }
                    else if((evt=="Docked"||evt=="Location"||evt=="CarrierJump") && Text(entry,"StationType").IndexOf("Carrier",StringComparison.OrdinalIgnoreCase)>=0)
                    {
                        object docked;
                        if(evt!="Docked" && (!entry.TryGetValue("Docked",out docked) || !(docked is bool) || !(bool)docked)) continue;
                        id=Text(entry,"MarketID"); name=Text(entry,"StationName"); callsign=Text(entry,"StationCallsign");
                        if(callsign.Length==0 && Regex.IsMatch(name,@"^[A-Za-z0-9]{3}-[A-Za-z0-9]{3}$")) callsign=name;
                    }
                    if(!CarrierInventory.ValidCarrier(id)||name.Length==0) continue;
                    DateTimeOffset seen; if(!DateTimeOffset.TryParse(Text(entry,"timestamp"),CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out seen)) continue;
                    string timestamp=seen.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ",CultureInfo.InvariantCulture);
                    CarrierInventory.Carrier carrier;
                    if(!found.TryGetValue(id,out carrier)) { carrier=new CarrierInventory.Carrier {id=id,name=name,callsign=callsign,lastSeen=timestamp,verified=true}; found[id]=carrier; }
                    else {
                        if(String.CompareOrdinal(timestamp,carrier.lastSeen)>0) carrier.lastSeen=timestamp;
                        if(callsign.Length>0) carrier.callsign=callsign;
                        if(name!=callsign && (carrier.name==carrier.callsign || String.CompareOrdinal(timestamp,carrier.lastSeen)>=0)) carrier.name=name;
                    }
                }
            }
        }
        return found.Values.OrderByDescending(x=>x.lastSeen).ThenBy(x=>x.name).ToList();
    }
}
