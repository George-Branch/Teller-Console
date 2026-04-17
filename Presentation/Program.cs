using Autofac;
using Autofac.Core;
using AutoMapper;
using Serilog;
using TellerDomain;

var _functions = new Functions();
var _validateFields = new ValidateFields();

 bool runProcess = true;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("TellerConsoleLog.txt", rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = new ContainerBuilder();
var container = _functions.RegisterDependencies(builder);

Deposit? deposit = null; 
Withdraw? withdraw = null;
IMapper mapper;
using var scope = container.BeginLifetimeScope();
try
{
    _ = scope.TryResolve<Deposit>(out deposit);
    _ = scope.TryResolve<Withdraw>(out withdraw);
    _ = scope.TryResolve<IMapper>(out mapper);
} catch (DependencyResolutionException ex)
{
    Log.Error(ex, $"Dependency resolution failed: {ex.Message}");
    Console.WriteLine("Application configuration error. Please contact support.");
    runProcess = false;
}
_functions.SetupDb();
             
while (runProcess == true)
{
    try
    {
        var transaction = new Transaction();

        do
        {
            Console.WriteLine();
            Console.WriteLine("_____________________________");
            Console.WriteLine("Processing new transaction...");
            Console.WriteLine("_____________________________");
            Console.WriteLine();

            Console.Write("Enter the Account Number or Q for quit: ");
            var acctNumber = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(acctNumber) || acctNumber.ToUpper() == "Q")
            {
                runProcess = false;
                break;
            }

            var success = _validateFields.ValidateAccountNumber(transaction, acctNumber);
            if (!success)
            {
                Log.Error($"Unable to get account number {acctNumber}");
                Console.WriteLine($"You have entered an invalid account number {acctNumber}");
                continue;
            }

            Console.Write("Enter the Account Type (1=Checking, 2=Savings) or Q for quit: ");
            var acctType = Console.ReadLine();
            success = _validateFields.ValidateAccountType(transaction, acctType);
            if (string.IsNullOrWhiteSpace(acctType) || acctType.ToUpper() == "Q")
            {
                runProcess = false;
                break;
            }
            if (!success) {
                Console.WriteLine($"Account Type {acctType} is not valid.");
                Log.Error($"Unable to get account type for account Type {acctType}");
                continue;
            }

            try
            {
                success = _functions.GetMemberByAccountNumberAndType(transaction);
            }
            catch (InvalidDataException ex)
            {
                Log.Error(ex.Message);
                Console.WriteLine(ex.Message);
                continue;
            }

            Console.Write("Enter the Transaction Type (1=Deposit, 2=Withdraw) or Q for quit: ");
            var transType = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(transType) || transType.ToUpper() == "Q")
            {
                runProcess = false;
                break;
            }

            success = _validateFields.ValidateTransactionType(transaction, transType);
 
            if (string.IsNullOrWhiteSpace(acctNumber) && !success)
            {
                Console.WriteLine($"Transaction Type {transType} is not valid.");
                Log.Error($"Unable to get transaction type for transaction Type {transType}");
                continue;
            }

            Console.Write("Enter the Transaction Amount or Q for quit: ");
            var transAmount = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(transAmount) || transAmount.ToUpper() == "Q")
            {
                runProcess = false;
                break;
            }
            success = _validateFields.ValidateTransactionAmount(transaction, transAmount);

            if (!success)
            {
                Log.Error($"Unable to get transaction amount for the entered value: {transaction.AmountToProcess}");
                Console.WriteLine($"Unable to get transaction amount for the entered value: {transaction.AmountToProcess}");
                continue;
            }

            Transaction actionObject;
            try
            {
                switch (transaction.TransactionType)
                {
                    case TransactionType.Deposit:
                        actionObject = deposit;
                        break;
                    case TransactionType.Withdrawal:
                        actionObject = withdraw;
                        break;
                    default:
                        Console.WriteLine($"Invalid transaction type - {transaction.TransactionType}.");
                        continue;
                }

                transaction = actionObject.ProcessTransaction(transaction);
                            
            } catch (InvalidDataException ex)
            {
                Log.Error(ex.Message);
                Console.WriteLine(ex.Message);
                continue;
            }
 
            Console.WriteLine($"The Beginning balance was {transaction.OriginalAccountBalance} and the ending balance is {transaction.AccountDTO.Balance}.");
        }
        while (runProcess);

        Log.CloseAndFlush();
    }

    catch (Exception ex)
    {
        Console.WriteLine($"{ex.Message}");
        continue;
    }

}
