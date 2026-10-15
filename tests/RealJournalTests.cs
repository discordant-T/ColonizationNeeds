using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
class RealJournalTests
{
    static void Main(string[] args)
    {
        var rows=(object[])new JavaScriptSerializer().DeserializeObject(File.ReadAllText(args[0])); int market=0,transfers=0;
        for(int i=0;i<rows.Length;i++) {
            var row=(Dictionary<string,object>)rows[i];var context=(Dictionary<string,object>)row["context"];var entry=(Dictionary<string,object>)row["entry"];
            var movements=CarrierInventory.Parse("Sample",i.ToString(),Convert.ToString(context["MarketID"]),Convert.ToString(context["StationType"]),entry);
            foreach(var m in movements) {if(m.reason.Length>0)throw new Exception("Real carrier movement required unexpected review");if(m.source=="Market")market++;else transfers++;}
            string kind=Convert.ToString(entry["event"]);
            if((kind=="MarketBuy"||kind=="MarketSell")&&movements.Count!=1)throw new Exception("Missing real market movement");
        }
        if(transfers==0)throw new Exception("No real transfer samples validated");
        Console.WriteLine("Real journal parser checks passed: "+rows.Length+" entries, "+market+" market movements, "+transfers+" direct transfers. Read-only; no uploads.");
    }
}
