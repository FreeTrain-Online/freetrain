// ExAV 2.1.1.12, 883cf7d/trunk/core/world/accounting/{AccountManager,AccountGenre,TransactionHistory}.cs.
// Original copyright Kohsuke Kawaguchi et al., LGPL-2.1-or-later.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// Original contribution identities; names and money formatting belong to presentation.
public enum AccountGenre { RailService, RoadService, Subsidiaries, Others }
public static class AccountGenres
{
    public static string SourceId(this AccountGenre genre) => genre switch {
        AccountGenre.RailService => "{AC30BB0B-044C-4784-A83D-FCB1F60B3CF2}",
        AccountGenre.RoadService => "{CC00A6D1-D078-4D3C-AFB2-EDC6CB9F4CB3}",
        AccountGenre.Subsidiaries => "{2A666F1A-9F40-4F67-98F4-DEAC1E55296D}",
        AccountGenre.Others => "{C0A9ABA5-801F-4AA6-93EA-6FF563C2B407}",
        _ => throw new ArgumentException("Unknown account genre.") };
}

public sealed record EconomicSettings(long BankruptcyIncrement = 10_000_000_000);
public sealed record AccountCharge(AccountGenre Genre, long Amount);

// An ordered sequence of source spend calls, including zero charges and repeated
// garage subtotals. Empty means no spend call. It is a quote, never a cash mutation.
public sealed class ConstructionBooking
{
    public ImmutableArray<AccountCharge> Charges { get; }
    public long Total { get; }
    public ConstructionBooking(IEnumerable<AccountCharge> charges)
    {
        Charges = charges.ToImmutableArray();
        long total = 0;
        foreach (var charge in Charges)
        {
            if (charge is null || !Enum.IsDefined(charge.Genre) || charge.Amount < 0)
                throw new ArgumentException("Invalid construction charge.");
            total = checked(total + charge.Amount);
        }
        Total = total;
    }
    public static ConstructionBooking None { get; } = new([]);
    public static ConstructionBooking Spend(long amount, AccountGenre genre = AccountGenre.RailService) => new([new(genre,amount)]);
}

// One record per source spend/earn call. Rescue funding is separate from genre sales:
// source transact() adds cash without calling history.earn(). WorldRevision groups
// one atomic operation; sequence preserves same-minute ordering for P8 reports.
public sealed record AccountTransaction(long Sequence, long WorldRevision, long AbsoluteMinute,
    string Operation, AccountGenre Genre, long Expenditure, long TopUps, long CashAfter, long Revenue = 0);
public sealed record ConstructionAccountState(long InitialCash, EconomicSettings Settings,
    ImmutableArray<AccountTransaction> Transactions);

public sealed class ConstructionAccounts
{
    public const long DefaultInitialCash = 15_000_000_000;
    public long InitialCash { get; }
    public EconomicSettings Settings { get; }
    public ImmutableArray<AccountTransaction> Transactions { get; }
    public long Cash { get; }
    public long TopUps { get; }
    public long NextSequence { get; }

    public ConstructionAccounts(long initialCash = DefaultInitialCash, EconomicSettings? settings = null)
        : this(initialCash, settings ?? new(), [], initialCash, 0, 1) { }

    private ConstructionAccounts(long initialCash, EconomicSettings settings,
        ImmutableArray<AccountTransaction> transactions, long cash, long topUps, long nextSequence)
    {
        if (settings.BankruptcyIncrement <= 0) throw new ArgumentException("Invalid bankruptcy increment.");
        InitialCash = initialCash; Settings = settings; Transactions = transactions;
        Cash = cash; TopUps = topUps; NextSequence = nextSequence;
    }

    public ConstructionAccountState Capture() => new(InitialCash, Settings, Transactions);
    public static ConstructionAccounts Restore(ConstructionAccountState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Settings is null || state.Transactions.IsDefault) throw new ArgumentException("Invalid accounts.");
        var accounts = new ConstructionAccounts(state.InitialCash,state.Settings);
        var cash = accounts.Cash; long topUps = 0, sequence = 1;
        AccountTransaction? previous = null;
        foreach (var entry in state.Transactions)
        {
            if (entry is null) throw new ArgumentException("Missing transaction.");
            ValidateContext(entry.WorldRevision,new(entry.AbsoluteMinute),entry.Operation,previous,allowSameRevision:true);
            if (entry.Expenditure < 0 || entry.Revenue < 0 || entry.Expenditure > 0 && entry.Revenue > 0 ||
                !Enum.IsDefined(entry.Genre)) throw new ArgumentException("Invalid transaction.");
            (cash, var rescues) = Spend(cash,checked(entry.Expenditure-entry.Revenue),state.Settings.BankruptcyIncrement);
            if (entry.Sequence != sequence || entry.TopUps != rescues || entry.CashAfter != cash)
                throw new ArgumentException("Inconsistent transaction history.");
            sequence = checked(sequence + 1); topUps = checked(topUps + rescues); previous = entry;
        }
        return new(state.InitialCash,state.Settings,state.Transactions,cash,topUps,sequence);
    }

    private static void ValidateContext(long worldRevision, GameTime time, string operation,
        AccountTransaction? previous, bool allowSameRevision)
    {
        if (worldRevision < 1 || string.IsNullOrWhiteSpace(operation) || operation.Length > 128 || operation.Any(char.IsControl) ||
            previous != null && (worldRevision < previous.WorldRevision || time.AbsoluteMinute < previous.AbsoluteMinute ||
                worldRevision == previous.WorldRevision && (!allowSameRevision || time.AbsoluteMinute != previous.AbsoluteMinute || operation != previous.Operation)))
            throw new ArgumentException("Invalid transaction context.");
    }

    public ConstructionAccounts Book(ConstructionBooking booking, long worldRevision, GameTime time, string operation)
        => Book(booking,worldRevision,time,operation,false);

    // One atomic operation can earn a fare before spending its operating cost.
    // Keep the two source calls ordered, including separate bankruptcy effects.
    public ConstructionAccounts EarnAndBook(long revenue, ConstructionBooking booking, long worldRevision,
        GameTime time, string operation)
        => Earn(revenue,worldRevision,time,operation).Book(booking,worldRevision,time,operation,true);

    private ConstructionAccounts Book(ConstructionBooking booking, long worldRevision, GameTime time, string operation,
        bool allowSameRevision)
    {
        ArgumentNullException.ThrowIfNull(booking);
        ValidateContext(worldRevision,time,operation,Transactions.IsEmpty ? null : Transactions[^1],allowSameRevision);
        if (booking.Charges.IsEmpty) return this;
        var entries = Transactions.ToBuilder(); var cash = Cash; var topUps = TopUps; var sequence = NextSequence;
        foreach (var charge in booking.Charges)
        {
            (cash, var rescueCount) = Spend(cash,charge.Amount,Settings.BankruptcyIncrement);
            topUps = checked(topUps + rescueCount);
            entries.Add(new(sequence,worldRevision,time.AbsoluteMinute,operation,charge.Genre,charge.Amount,rescueCount,cash));
            sequence = checked(sequence + 1);
        }
        return new(InitialCash,Settings,entries.ToImmutable(),cash,topUps,sequence);
    }

    public ConstructionAccounts Earn(long amount, long worldRevision, GameTime time, string operation,
        AccountGenre genre = AccountGenre.RailService)
    {
        if (amount < 0 || !Enum.IsDefined(genre)) throw new ArgumentException("Invalid revenue.");
        ValidateContext(worldRevision,time,operation,Transactions.IsEmpty ? null : Transactions[^1],allowSameRevision:false);
        var (cash, rescues) = Spend(Cash,-amount,Settings.BankruptcyIncrement);
        var entry = new AccountTransaction(NextSequence,worldRevision,time.AbsoluteMinute,operation,genre,0,rescues,cash,amount);
        return new(InitialCash,Settings,Transactions.Add(entry),cash,checked(TopUps+rescues),checked(NextSequence+1));
    }

    // Closed form of the source while(cash < 0) loop. Widening the rescue
    // calculation avoids negating Int64.MinValue and billions of loop iterations;
    // the actual subtraction and final counters must still fit signed Int64.
    internal static (long Cash, long TopUps) Spend(long cash, long amount, long increment)
    {
        var remainder = checked(cash - amount);
        var count = remainder < 0 ? (-(Int128)remainder + increment - 1) / increment : 0;
        return (checked((long)(remainder + count * increment)),checked((long)count));
    }
}
