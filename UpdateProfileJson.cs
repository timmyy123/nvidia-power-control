using System;
using System.IO;

class Program {
    static void Main() {
        string path = @"C:\Users\timmy\AppData\Local\mVolt+\profiles\95.03.3e.00.0a-pci-271710de-14261462-00002717-b01-s00.json";
        if (File.Exists(path)) {
            string text = File.ReadAllText(path);
            text = text.Replace("\"xoc_enabled\": false", "\"xoc_enabled\": true");
            text = text.Replace("\"domain_rail_extended_range\": false", "\"domain_rail_extended_range\": true");
            text = text.Replace("\"risk_acknowledged\": false", "\"risk_acknowledged\": true");
            text = text.Replace("\"core_voltage_demand_enabled\": false", "\"core_voltage_demand_enabled\": true");
            File.WriteAllText(path, text);
            Console.WriteLine("Profile JSON updated.");
        }
    }
}
