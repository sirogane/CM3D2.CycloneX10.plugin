using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityInjector.Attributes;

using CycloneX10Csharp;
using System.Xml.Serialization;

namespace CM3D2.CycloneX10.plugin
{
    [PluginFilter("CM3D2x64"), PluginFilter("CM3D2x86"), PluginFilter("CM3D2VRx64")]
    [PluginName(CycloneX10.PluginName), PluginVersion(CycloneX10.Version)]
    public class CycloneX10 : UnityInjector.PluginBase
    {
        private const string PluginName = "CM3D2 CycloneX10 Plugin";
        private const string Version = "0.1.1.0";


        //XMLファイルの読み込み先
        private readonly string XmlFileDirectory = Application.dataPath + "/../UnityInjector/Config/CycloneX10Xml/";

        //各種設定項目
        private readonly float TimePerInit = 1.00f;
        private readonly float WaitFirstInit = 5.00f;

        //初期化完了かどうか
        private bool InitCompleted = false;

        //動作中のステータス
        private string yotogi_group_name = "";          //夜伽グループ名
        private string yotogi_name = "";                //夜伽名
        private int iLastExcite = 0;                    //興奮値
        private Yotogi.ExcitementStatus yExciteStatus;  //興奮ステータス
        private YotogiPlay.PlayerState bInsertFuck = YotogiPlay.PlayerState.Normal;               //挿入状態かどうか
        private string Personal="";

        // CM3D2関連の参照
        private int sceneLevel;//シーンレベル
        private Maid maid;
        private YotogiManager yotogiManager;
        private YotogiPlayManager yotogiPlayManager;
        private Action<Yotogi.SkillData.Command.Data> orgOnClickCommand;

        //CycloneX10関連
        private CycloneX10Class cycloneX10 = new CycloneX10Class();
        private static bool CycloneGUI = false;
        private Rect windowRect = new Rect(20, 20, 120, 50);
        private int NowPattern = 0;
        private int NowLevel = 0;

        //サイクロン用の設定ファイル郡
        private CycloneX10Config.YotogiItem YotogiItem = null;
        private SortedDictionary<string, CycloneX10Config> CycloneX10ConfigDictionay = new SortedDictionary<string, CycloneX10Config>();
        private Dictionary<string, CycloneX10Config.LevelItem> CycloneX10LevelsDict = new Dictionary<string, CycloneX10Config.LevelItem>();

        //コルーチン
        private IEnumerator CycloneEnum = null;

        #region MonoBehaviour methods
        public void Start()
        {
            //設定用のディレクトリを生成する
            if (!System.IO.Directory.Exists(XmlFileDirectory))
            {
                System.IO.Directory.CreateDirectory(XmlFileDirectory);
                Debug.Log("ディレクトリ生成:" + XmlFileDirectory);
            }

            //デッバク用ログ初期設定
            DebugManager.DebugMode = false;
        }

        public void Update()
        {
            if (sceneLevel == 14)
            {
                //ログを表示する
                if (Input.GetKeyDown(KeyCode.F10))
                {
                    DebugManager.DebugMode = !DebugManager.DebugMode;
                }
                //サイクロンX10関連のダイアログ表示
                if (Input.GetKeyDown(KeyCode.F11))
                {
                    CycloneGUI = !CycloneGUI;
                }
            }
        }

        public void OnGUI()
        {
            //デバッグ機能
            if (InitCompleted && sceneLevel == 14)
            {
                //ログ表示
                DebugManager.GUIText();
                //サイクロンX10関連のデバッグ用ウィンドウ
                if (CycloneGUI)
                    windowRect = GUILayout.Window(0, windowRect, GUIWindow, "CycloneX10");
            }
        }

        public void OnApplicationQuit()
        {
            cycloneX10Init();
        }

        public void cycloneX10Init()
        {
            //Stopする
            cycloneX10.SetPatternAndLevel(0, 0);

            //変数群初期化
            yotogi_group_name = "";
            yotogi_name = "";
            iLastExcite = 0;
            yExciteStatus = 0;
            bInsertFuck = YotogiPlay.PlayerState.Normal;
            Personal = "";

            NowPattern = 0;
            NowLevel = 0;
        }

        //シーンがロードされた場合
        public void OnLevelWasLoaded(int level)
        {
            //夜伽シーンの場合初期化をする
            if (level == 14)
            {
                //起動時に読み込み
                LoadCycloneXMLFile();
                //初期化
                StartCoroutine(initCoroutine(TimePerInit));
            }
            cycloneX10Init();

            //読み込んだシーンレベルを保存
            sceneLevel = level;
        }
        #endregion

        #region MonoBehaviour Coroutine

        private IEnumerator initCoroutine(float waitTime)
        {
            yield return new WaitForSeconds(WaitFirstInit);
            while (!(InitCompleted = Yotogi_initialize())) yield return new WaitForSeconds(waitTime);
            DebugManager.Log("Initialization complete [ Load SeenLevel:" + sceneLevel.ToString() + "]");
        }

        private IEnumerator CycloneCoroutine(int iLastExcite, CycloneX10Config.YotogiItem YotogiItem, Dictionary<string, CycloneX10Config.LevelItem> CycloneX10PattanDict, bool InsertFlg, string Personal)
        {
            //興奮状態のステータス
            yExciteStatus = YotogiPlay.GetExcitementStatus(iLastExcite);

            foreach (CycloneX10Config.Control Item in YotogiItem.ControlData)
            {
                //性格の指定があるかどうか(未指定の場合はそのまま実行)
                if (Item.Personal == "" || Item.Personal == Personal)
                {
                    //挿入時に挿入フラグがあった場合もしくはそれ以外
                    if ((Item.Insert && InsertFlg) || Item.Insert == false)
                    {
                        //現在のPatternとLevel
                        int SetPattan = cycloneX10.pattern;
                        int SetLevel = cycloneX10.level;

                        //Patternの定義があれば更新
                        if (-1 < Item.Pattern)
                        {
                            SetPattan = Clamp(Item.Pattern, 0, 9);
                        }

                        //Patternの定義があれば更新
                        if (-1 < Item.Level)
                        {
                            SetLevel = Clamp(Item.Level, 0, 9);
                        }
                        //LevelNameの定義がある場合
                        if (Item.LvName != "")
                        {
                            if (CycloneX10PattanDict.ContainsKey(Item.LvName))
                            {
                                //興奮値を元にLevelを更新
                                SetLevel = Clamp(GetLevel(yExciteStatus, CycloneX10PattanDict[Item.LvName]), 0, 9);
                            }
                            else
                            {
                                DebugManager.Log("LevelNameの定義が見つかりません");
                            }
                        }

                        //ディレイ
                        if (0.0f < Item.Delay)
                        {
                            yield return new WaitForSeconds(Item.Delay);
                        }

                        //振動を開始する
                        if (SetLevel != cycloneX10.level || SetPattan != cycloneX10.pattern)
                        {
                            //Cycloneの振動処理
                            cycloneX10.SetPatternAndLevel(SetPattan, SetLevel);
                            //GUI用に更新をする。
                            NowPattern = cycloneX10.pattern;
                            NowLevel = cycloneX10.level;
                        }

                        //ログを追加
                        DebugManager.Log("cycloneX10 : [Pattern:" + cycloneX10.pattern + "][Level:" + cycloneX10.level + "][Delay:" + Item.Delay + "][Time:" + Item.Time + "]");

                        //継続タイム
                        if (0.0f < Item.Time)
                        {
                            yield return new WaitForSeconds(Item.Time);
                        }
                    }
                }
            }
        }
        #endregion

        #region MonoBehaviour GUI関連

        /// <summary>
        /// Cyclone用の操作Window
        /// </summary>
        /// <param name="windowID"></param>
        private void GUIWindow(int windowID)
        {
            GUILayout.BeginHorizontal();
            {
                if (cycloneX10.IsDeviceEnable)
                {
                    GUILayout.Label("接続状態: 接続中");
                }
                else
                {
                    GUILayout.Label("接続状態: 未接続");
                }
                if (GUILayout.Button("XML再読み込み"))
                {
                    LoadCycloneXMLFile();
                }

                //通常このXML出力機能は使わない
                //if (GUILayout.Button("出力"))
                //{
                //    CreateAllYotogiXML();
                //}
                
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Pattern");

            GUILayout.BeginHorizontal();
            {
                for (int i = 0; i < 10; i++)
                {
                    if (GUILayout.Toggle(i == NowPattern, i.ToString()))
                    {
                        NowPattern = i;
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Level");

            GUILayout.BeginHorizontal();
            {
                for (int i = 0; i < 10; i++)
                {
                    if (GUILayout.Toggle(i == NowLevel, i.ToString()))
                    {
                        NowLevel = i;
                    }
                }
                if (NowLevel != cycloneX10.level || NowPattern != cycloneX10.pattern)
                {
                    cycloneX10.SetPatternAndLevel(NowPattern, NowLevel);
                    NowPattern = cycloneX10.pattern;
                    NowLevel = cycloneX10.level;
                    DebugManager.Log("SetPatternAndLevel:" + cycloneX10.pattern + "," + cycloneX10.level);
                }
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("ポーズ:" + cycloneX10.IsPause))
            {
                cycloneX10.Pause();
                DebugManager.Log("ポーズ:" + cycloneX10.IsPause.ToString());
            }

            GUILayout.Label("夜伽グループ:" + yotogi_group_name);
            GUILayout.Label("夜伽コマンド:" + yotogi_name);
            GUILayout.Label("興奮値　　　:" + iLastExcite.ToString() + "[" + yExciteStatus+"]");
            GUILayout.Label("挿入状態　　:" + bInsertFuck.ToString());
            GUILayout.Label("メイド性格　:" + Personal);
            GUI.DragWindow();
        }

        /// <summary>
        /// 画面上に常に表示をするデバッグ機能
        /// </summary>
        private static class DebugManager
        {
            public static bool DebugMode
            {
                get { return _DebugMode; }
                set { _DebugMode = value; }
            }
            //デバッグの最大行数
            private const int MaxDebugText = 10;

            private static bool _DebugMode = false;
            private static Queue<string> DebugTextList = new Queue<string>();
            private static Rect TextAreaRect = new Rect(10, 10, Screen.width / 2, Screen.height - 20);

            //デバッグ情報として出力する内容
            public static void Log(string DebugText)
            {
                if (MaxDebugText < DebugTextList.Count)
                {
                    //先頭の物を削除
                    DebugTextList.Dequeue();
                    DebugTextList.Enqueue(DebugText);
                }
                else
                {
                    DebugTextList.Enqueue(DebugText);
                }
            }
            //クリア
            public static void Clear()
            {
                DebugTextList.Clear();
            }

            //OnGUI上で実行すること
            public static void GUIText()
            {
                if (DebugMode)
                {
                    GUILayout.BeginArea(TextAreaRect);
                    foreach (string log in DebugTextList)
                    {
                        GUILayout.Label(log);
                    }
                    GUILayout.EndArea();
                }
            }

        }
        #endregion

        #region UnityInjector関連
        private bool Yotogi_initialize()
        {
            //初期化を行う
            cycloneX10.OpenDevice();

            //メイドを取得
            this.maid = GameMain.Instance.CharacterMgr.GetMaid(0);
            if (!this.maid) return false;

            // 夜伽コマンドフック
            {
                this.yotogiManager = getInstance<YotogiManager>();
                if (!this.yotogiManager) return false;
                this.yotogiPlayManager = getInstance<YotogiPlayManager>();
                if (!this.yotogiPlayManager) return false;

                YotogiCommandFactory cf = getFieldValue<YotogiPlayManager, YotogiCommandFactory>(this.yotogiPlayManager, "command_factory_");
                if (IsNull(cf)) return false;

                try
                {
                    //YotogiPlayManagerのコールバック
                    cf.SetCommandCallback(new YotogiCommandFactory.CommandCallback(this.OnYotogiPlayManagerOnClickCommand));
                }
                catch (Exception ex)
                {
                    DebugManager.Log(string.Format("Error - SetCommandCallback() : {0}", ex.Message));
                    return false;
                }

                this.orgOnClickCommand = getMethodDelegate<YotogiPlayManager, Action<Yotogi.SkillData.Command.Data>>(this.yotogiPlayManager, "OnClickCommand");
                if (IsNull(this.orgOnClickCommand)) return false;
            }
            return true;
        }

        public void OnYotogiPlayManagerOnClickCommand(Yotogi.SkillData.Command.Data command_data)
        {
            YotogiPlay.PlayerState OldPlayerState = bInsertFuck;

            //実際の動作をする
            orgOnClickCommand(command_data);

            //メイドの性格を取得
            Personal = this.maid.Param.status.personal.ToString();

            //夜伽グループ名
            yotogi_group_name = command_data.basic.group_name;
            //夜伽コマンド名
            yotogi_name = command_data.basic.name;
            //興奮値
            iLastExcite = maid.Param.status.cur_excite;
            //興奮状態のステータス
            yExciteStatus = YotogiPlay.GetExcitementStatus(iLastExcite);
            //挿入状態かどうか
            bInsertFuck = getFieldValue<YotogiPlayManager, YotogiPlay.PlayerState>(this.yotogiPlayManager, "player_state_");

            //PlayerStateがNormalからInsertになる場合
            bool InsertFlg = (OldPlayerState == YotogiPlay.PlayerState.Normal && bInsertFuck == YotogiPlay.PlayerState.Insert);

            //Cycloneを実行する
            CycloneX10Events(yotogi_group_name, yotogi_name, iLastExcite, InsertFlg, Personal);
        }
        #endregion

        #region CycloneX10関連
        private void CycloneX10Events(string yotogi_group_name, string yotogi_name, int iLastExcite, bool InsertFlg ,string Personal)
        {
            //前回のコルーチンが走っている場合は停止をする
            if (CycloneEnum != null) { StopCoroutine(CycloneEnum); }

            YotogiItem = null;
            CycloneX10LevelsDict.Clear();
            if (CycloneX10ConfigDictionay.ContainsKey(yotogi_group_name))
            {
                //振動パターンのDictionayを生成
                foreach (CycloneX10Config.LevelItem Item in CycloneX10ConfigDictionay[yotogi_group_name].LevelList)
                {
                    if (!CycloneX10LevelsDict.ContainsKey(Item.LvName))
                    {
                        CycloneX10LevelsDict.Add(Item.LvName, Item);
                    }
                    else
                    {
                        Debug.Log("Warning : LevelNameが重複しています。[" + Item.LvName + "]");
                    }
                }
                //設定ファイルを確定する
                foreach (CycloneX10Config.YotogiItem Item in CycloneX10ConfigDictionay[yotogi_group_name].YotogiCXConfig.YotogiList)
                {
                    if (Item.Yotogi_Name == yotogi_name)
                    {
                        YotogiItem = Item;
                        break;
                    }
                }
                if (YotogiItem != null)
                {
                    DebugManager.Log("実行:" + YotogiItem.Yotogi_Name);

                    //コルーチンを開始する
                    CycloneEnum = CycloneCoroutine(iLastExcite, YotogiItem, CycloneX10LevelsDict, InsertFlg, Personal);
                    StartCoroutine(CycloneEnum);
                }
            }
        }

        //導入されている全夜伽コマンドデータ用の設定ファイルを一括作成
        void CreateAllYotogiXML()
        {
            for (int cat = 0; cat < (int)Yotogi.Category.MAX; cat++)
            {
                SortedDictionary<int, Yotogi.SkillData> data = Yotogi.skill_data_list[cat];
                foreach (Yotogi.SkillData sd in data.Values)
                {
                    CycloneX10Config XML = new CycloneX10Config();
                    XML.EditInformation.EditName = "UserName";
                    XML.EditInformation.TimeStamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    XML.EditInformation.Comment = "";

                    XML.YotogiCXConfig.GroupName = sd.name;
                    XML.LevelList.Clear();
                    XML.LevelList.Add(PatternItem("STOP", 0, 0, 0, 0));
                    XML.LevelList.Add(PatternItem("PreSet1", 1, 1, 3, 5));
                    XML.LevelList.Add(PatternItem("PreSet2", 1, 2, 3, 4));
                    XML.LevelList.Add(PatternItem("PreSet3", 1, 3, 5, 7));

                    foreach (var comData in sd.command.data)
                    {
                        CycloneX10Config.YotogiItem YotogiListData = new CycloneX10Config.YotogiItem();

                        YotogiListData.Yotogi_Name = comData.basic.name;
                        YotogiListData.ControlData.Add(ControlItem(0f, "STOP"));

                        XML.YotogiCXConfig.YotogiList.Add(YotogiListData);
                    }

                    XMLWriter<CycloneX10Config>(XmlFileDirectory + sd.name + ".xml", XML);
                }
            }
        }

        private CycloneX10Config.LevelItem PatternItem(string Name, int LV0, int LV1, int LV2, int LV3)
        {
            CycloneX10Config.LevelItem PItem = new CycloneX10Config.LevelItem();
            PItem.LvName = Name;
            PItem.Lv0 = LV0;
            PItem.Lv1 = LV1;
            PItem.Lv2 = LV2;
            PItem.Lv3 = LV3;
            return PItem;
        }
        private CycloneX10Config.Control ControlItem(float diray , string Name)
        {
            CycloneX10Config.Control Cont = new CycloneX10Config.Control();
            Cont.Delay = diray;
            Cont.LvName = Name;
            return Cont;
        }

        /// <summary>
        /// 振動設定用のXMLファイル
        /// </summary>
        private void LoadCycloneXMLFile()
        {
            Debug.Log("読み込み開始");
            CycloneX10ConfigDictionay.Clear();
            string[] files = System.IO.Directory.GetFiles(XmlFileDirectory, "*.xml", System.IO.SearchOption.AllDirectories);
            foreach (string file in files)
            {
                try
                {
                    if (System.IO.File.Exists(file))
                    {
                        CycloneX10Config XML = XMLLoader<CycloneX10Config>(file);
                        if (!CycloneX10ConfigDictionay.ContainsKey(XML.YotogiCXConfig.GroupName))
                        {
                            CycloneX10ConfigDictionay.Add(XML.YotogiCXConfig.GroupName, XML);
                        }
                    }
                }
                catch (Exception err)
                {
                    //エラーが有った場合のみエラー内容を表示
                    Debug.Log(System.IO.Path.GetFileName(file) + ":LoadError [" + err + "] ");
                }
            }
            Debug.Log("CycloneX10の設定ファイル " + CycloneX10ConfigDictionay.Count + "個 読み込み完了");
        }

        #endregion

        #region 各種関数群
        /// <summary>
        /// XMLデータの読み込み
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <returns></returns>
        internal static T XMLLoader<T>(string path)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));

            FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            StreamReader reader = new System.IO.StreamReader(stream, new System.Text.UTF8Encoding(false));
            T load = (T)serializer.Deserialize(reader);
            reader.Close();

            return load;
        }
        /// <summary>
        /// XMLデータの書き込み
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <param name="save"></param>
        public static void XMLWriter<T>(string path, T save)
        {
            //XMLファイルに保存する
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            System.IO.StreamWriter writer = new System.IO.StreamWriter(path, false, new System.Text.UTF8Encoding(false));
            serializer.Serialize(writer, save);
            writer.Close();
        }


        //ゲームオブジェクトの検索と取得
        internal static T getInstance<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectOfType(typeof(T)) as T;
        }
        //IsNUll
        internal static bool IsNull<T>(T t) where T : class
        {
            return (t == null) ? true : false;
        }

        internal static TResult getFieldValue<T, TResult>(T inst, string name)
        {
            if (inst == null) return default(TResult);

            FieldInfo field = getFieldInfo<T>(name);
            if (field == null) return default(TResult);

            return (TResult)field.GetValue(inst);
        }
        internal static FieldInfo getFieldInfo<T>(string name)
        {
            BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

            return typeof(T).GetField(name, bf);
        }

        internal static TResult getMethodDelegate<T, TResult>(T inst, string name)
            where T : class
            where TResult : class
        {
            return Delegate.CreateDelegate(typeof(TResult), inst, name) as TResult;
        }

        private int GetLevel(Yotogi.ExcitementStatus Status, CycloneX10Config.LevelItem LevelItem)
        {
            try
            {
                switch (Status)
                {
                    case Yotogi.ExcitementStatus.Minus:
                        {
                            return LevelItem.Lv0;
                        }
                    case Yotogi.ExcitementStatus.Small:
                        {
                            return LevelItem.Lv1;
                        }
                    case Yotogi.ExcitementStatus.Medium:
                        {
                            return LevelItem.Lv2;
                        }
                    case Yotogi.ExcitementStatus.Large:
                        {
                            return LevelItem.Lv3;
                        }
                    default:
                        {
                            return -1;
                        }
                }
            }
            catch
            {
                Debug.Log("Error:GetLevel");
                return -1;
            }
        }

        /// 値の最大最小を制限する
        private static int Clamp(int value, int Min, int Max)
        {
            if (value < Min)
            {
                return Min;
            }
            else if (Max < value)
            {
                return Max;
            }
            else
            {
                return value;
            }
        }

        #endregion

    }
}