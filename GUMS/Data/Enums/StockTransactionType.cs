namespace GUMS.Data.Enums;

public enum StockTransactionType
{
    Purchase,       // leader bought stock; qty positive
    Award,          // given to a member; qty negative
    VisitorAward,   // given to a visitor; qty negative; notes required
    ManualIn,       // returned, donated, etc.; qty positive; notes required
    ManualOut,      // lost, damaged; qty negative; notes required
    Adjustment      // stocktake correction; can be +/-; notes required
}
