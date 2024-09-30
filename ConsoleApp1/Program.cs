
//var plaza = new PlazaConnector("02mMLX144T2yxnfzEUrCjUKzXKciQKJ", test: false, appname: "osaApplication", 20)
using PlazaEngine.Engine;
using PlazaEngine.Entity;

// REAL ACCOUNT CONNECTOR
var plaza = new PlazaConnector("02mMLX144T2yxnfzEUrCjUKzXKciQKJ", test: false, appname: "osaApplication")
{
    Limit = 30,
    LoadTicksFromStart = false,
};


plaza.UpdateSecurity += Plaza_UpdateSecurity;

void Plaza_UpdateSecurity(Security security)
{
    //throw new NotImplementedException();
}

plaza.Connect();

Console.ReadKey();

