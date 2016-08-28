using System.Collections.Generic;
public class CycloneX10Config
{
    public editInformation EditInformation = new editInformation();
    public List<LevelItem> LevelList = new List<LevelItem>();
    public YotogiConfig YotogiCXConfig = new YotogiConfig();

    /// <summary>
    /// 編集者情報
    /// </summary>
    /// 設定上必須ではないので省略可能
    public class editInformation
    {
        public string EditName = "";  //編集者名
        public string TimeStamp = ""; //編集日 20160825など
        public string Comment = "";   //コメント
    }
    /// <summary>
    /// CycronX10用設定
    /// </summary>
    public class YotogiConfig
    {
        //夜伽グループ名
        public string GroupName = "";
        public List<YotogiItem> YotogiList = new List<YotogiItem>();
    }

    public class YotogiItem
    {
        //夜伽名
        public string Yotogi_Name = "";
        public List<Control> ControlData = new List<Control>();
    }
    //Cycloneの動作設定情報
    public class Control
    {
        [System.Xml.Serialization.XmlText()]
        public string Comment = "";         //コメント(必須ではない)

        [System.Xml.Serialization.XmlAttribute("Pattern")]
        public int Pattern = -1;            //振動Pattern

        [System.Xml.Serialization.XmlAttribute("Level")]
        public int Level = -1;               //振動Pattern

        [System.Xml.Serialization.XmlAttribute("Delay")]
        [System.ComponentModel.DefaultValue(0f)]
        public float Delay = 0f;            //振動開始までのdelay

        [System.Xml.Serialization.XmlAttribute("Time")]
        [System.ComponentModel.DefaultValue(0f)]
        public float Time = 0f;             //処理継続時間

        [System.Xml.Serialization.XmlAttribute("LvName")]
        [System.ComponentModel.DefaultValue("")]
        public string LvName = "";       //振動パターン名

        [System.Xml.Serialization.XmlAttribute("Insert")]
        [System.ComponentModel.DefaultValue(false)]
        public bool Insert = false;       //挿入時のみフラグ

        [System.Xml.Serialization.XmlAttribute("Personal")]
        [System.ComponentModel.DefaultValue("")]
        public string Personal = "";       //性格

    }

    public class LevelItem
    {
        [System.Xml.Serialization.XmlText()]
        public string LvName = "";

        [System.Xml.Serialization.XmlAttribute("Lv0")]
        public int Lv0 = 0;     //Minus

        [System.Xml.Serialization.XmlAttribute("Lv1")]
        public int Lv1 = 0;     //Small 

        [System.Xml.Serialization.XmlAttribute("Lv2")]
        public int Lv2 = 0;     //Medium 

        [System.Xml.Serialization.XmlAttribute("Lv3")]
        public int Lv3 = 0;     //Large 

    }
  
}