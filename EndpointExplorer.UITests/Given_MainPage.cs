namespace EndpointExplorer.UITests;
// stub/example - not sure on use
public class Given_MainPage : TestBase
{
    [Test]
    public async Task When_SmokeTest()
    {
        // NOTICE
        // To run UITests, Run the WASM target without debugger. Note
        // the port that is being used and update the Constants.cs file
        // in the UITests project with the correct port number.

        await Task.Delay(5000);

        Query fileMenu = q => q.All().Marked("File");
        App.WaitForElement(fileMenu);

        // Take a screenshot and add it to the test results
        TakeScreenshot("After launch");
    }
}
