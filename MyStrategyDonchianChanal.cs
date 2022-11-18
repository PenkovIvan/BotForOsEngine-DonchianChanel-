using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class MyStrategyDonchianChanal : BotPanel
{
    //ЦУ1
    //1. Когда цена косается верхней границы-открываем LONG
    
    //ЦУ2
    //1. Канал настраиваем на 200-дневный период для определения долгосрочного тренда
    //2. Если цена нах выше средней линии-то ищем вход в LONG
   
    //ЦУ3
    //1. Если тренд восходящий-используем нижнюю линия для установки стоп-лосса
    
    //Вход 50% отката хвоста пин бара


    private BotTabSimple _bot;
    private Atr _atr;
    private DonchianChannel _donchianChannel;

    public StrategyParameterInt Stop { get; set; }
    public StrategyParameterInt Slippage { get; set; }
    public decimal Value { get; set; }
    public StrategyParameterInt Profit { get; set; }
    public StrategyParameterString IsOnOff { get; set; }
    public StrategyParameterDecimal OptimalF { get; set; }
    //public bool IsOnRobot { get; set; } = true;

    public List<Position> Positions { get; set; }

    public MyStrategyDonchianChanal(string name, StartProgram startProgram) : base(name, startProgram)
    {

        IsOnOff = CreateParameter("Включить", "НЕТ", new string[] { "ДА", "НЕТ" });
        Stop = CreateParameter("Стоп", 20, 20, 100, 10);
        Slippage = CreateParameter("Проскальзывание", 1, 2, 10, 1);
        Profit = CreateParameter("Прибыль", 10, 50, 1000, 10);
        OptimalF = CreateParameter("Коэффициент расчета плеча", 0.1m, 0.35m, 1, 0.1m);
        //IsOnRobot = true;
        //Load();

        TabCreate(BotTabType.Simple);
        _bot = TabsSimple[0];

        _donchianChannel = new DonchianChannel("DC", false)
        {
            Lenght = 200,
            ColorAvg = System.Drawing.Color.Coral,
            ColorDown = System.Drawing.Color.Purple,
            ColorUp = System.Drawing.Color.Purple
        };
        _donchianChannel = (DonchianChannel)_bot.CreateCandleIndicator(_donchianChannel, "Prime");
        _donchianChannel.Save();

        _atr = new Atr("Atr", false);
        _atr = (Atr)_bot.CreateCandleIndicator(_atr, "NewArea");
        _atr.Save();

        _bot.CandleFinishedEvent += _bot_CandleFinishedEvent;
        _bot.PositionOpeningSuccesEvent += _bot_PositionOpeningSuccesEvent;
    }

    private void _bot_PositionOpeningSuccesEvent(Position position)
    {
        _bot.CloseAtStop(
           position,
           position.EntryPrice - Stop.ValueInt * _bot.Securiti.PriceStep,
             position.EntryPrice - Stop.ValueInt * _bot.Securiti.PriceStep - Slippage.ValueInt * _bot.Securiti.PriceStep - 2 * _atr.Values[_atr.Values.Count - 1]
          );

        _bot.CloseAtProfit(position, position.EntryPrice + Profit.ValueInt * _bot.Securiti.PriceStep,
               position.EntryPrice + Profit.ValueInt * _bot.Securiti.PriceStep + Slippage.ValueInt * _bot.Securiti.PriceStep);
    }

    private void _bot_CandleFinishedEvent(List<Candle> candles)
    {
        Positions = _bot.PositionsOpenAll;
        decimal lastCandleClose = candles[candles.Count - 1].Close;//цена закрытия последней свечи
        decimal lastCandleOpen = candles[candles.Count - 1].Open;//Цена открытия последней свечи
        decimal lastCandleHigh = candles[candles.Count - 1].High;//максимум последней свечи
        decimal lastCandleLow = candles[candles.Count - 1].Low;//минимум последней свечи
        decimal lastDonchianChanelAverage = _donchianChannel.ValuesAvg[_donchianChannel.ValuesAvg.Count - 1];//последнее значение скользящей средней канала Дончиана
        decimal lastDonchianChanelValuesUp = _donchianChannel.ValuesUp[_donchianChannel.ValuesUp.Count - 1];//последнее значение верхней границы канала Дончиана

        decimal percent50RollbackOfThePinBarTail = (lastCandleClose + lastCandleLow) / 2;//для входа 50% откате от пинабра
        decimal depositValueNow = _bot.Portfolio.ValueCurrent;//текущая величина депозита
        decimal lastPriceInstrument = lastCandleClose;//текущая цена инструмента
        Value = Convert.ToInt32(depositValueNow * OptimalF.ValueDecimal / lastPriceInstrument);//Рсчет объема депозита для входа в позицию (коэф оптимаотное F по умолячанию равен 0,5)
                                                                                               // Value=1;
                                                                                               //Проверки
        if (IsOnOff.ValueString != "ДА" /*|| IsOnRobot == false*/)
        {
            return;
        }
        if (Positions != null && Positions.Count != 0)
        {
            return;
        }
        if (Positions != null && Positions.Count != 0)
        {
            if (Positions[0].State != PositionStateType.Open)
            {
                return;
            }
        }
        if (_atr.Lenght > candles.Count || _donchianChannel.Lenght > candles.Count)
        {
            return;
        }
        if (candles[candles.Count - 1].TimeStart.Hour < 11 || candles[candles.Count - 1].TimeStart.DayOfWeek == DayOfWeek.Friday && candles[candles.Count - 1].TimeStart.Hour > 17)
        {
            return;
        }

        if (Positions == null || Positions.Count == 0)
        {
            //logic Long
            if (lastCandleClose > lastDonchianChanelAverage ||
                lastCandleClose >= lastDonchianChanelValuesUp &&
                lastCandleClose >= lastCandleHigh - ((lastCandleHigh - lastCandleLow) / 3) &&
                lastCandleOpen >= lastCandleHigh - ((lastCandleHigh - lastCandleLow) / 3) //&&
               /* lastCandleClose > lastCandleOpen*/)
            {
                _bot.BuyAtLimit(Value, percent50RollbackOfThePinBarTail + Slippage.ValueInt);
            }

        }

    }
    //Если нужно сохранить настройки в файл и сделать настройки через WPF
    //public void Save()
    //{
    //    try
    //    {
    //        using (StreamWriter writer = new StreamWriter(/*@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"*/@"C:\TESTDIR\" + NameStrategyUniq + @"SettingsBot.txt"))
    //        {
    //            writer.WriteLine(Stop);
    //            writer.WriteLine(Slippage);
    //            writer.WriteLine(Profit);
    //            writer.WriteLine(IsOnRobot);
    //            writer.WriteLine(OptimalF);
    //            writer.Close();
    //        }
    //    }
    //    catch (Exception)
    //    {

    //        //ignore
    //    }
    //}
    //private void Load()
    //{
    //    if (!File.Exists(/*@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"*/@"C:\TESTDIR\" + NameStrategyUniq + @"SettingsBot.txt"))
    //    {
    //        return;
    //    }
    //    try
    //    {
    //        using (StreamReader reader = new StreamReader(/*@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"*/@"C:\TESTDIR\" + NameStrategyUniq + @"SettingsBot.txt"))
    //        {
    //            Stop.ValueInt = Convert.ToInt32(reader.ReadLine());
    //            Slippage.ValueInt = Convert.ToInt32(reader.ReadLine());
    //            Profit.ValueInt = Convert.ToInt32(reader.ReadLine());
    //            IsOnRobot = Convert.ToBoolean(reader.ReadLine());
    //            OptimalF.ValueDecimal = Convert.ToDecimal(reader.ReadLine());

    //            reader.Close();
    //        }
    //    }
    //    catch (Exception)
    //    {

    //        //ignore
    //    }
    //}

    public override string GetNameStrategyType()
    {
        return "MyStrategyDonchianChanal";
    }

    public override void ShowIndividualSettingsDialog()
    {
        //MyStrategyDonchianChanalUi chanalUi = new MyStrategyDonchianChanalUi(this);
        //chanalUi.ShowDialog();
    }
}
