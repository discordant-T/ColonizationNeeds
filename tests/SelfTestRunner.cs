using System;
class SelfTestRunner
{
    [STAThread] static int Main()
    { try { ColonizationNeeds.Main(new[] {"--self-test"}); ColonizationNeeds.Main(new[] {"--test-key-protection"}); Console.WriteLine("Application self-tests and credential encryption check passed."); return 0; } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; } }
}
