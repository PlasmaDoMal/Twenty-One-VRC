using UdonSharp;
using UnityEngine;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class WarehouseLeaderboardController : UdonSharpBehaviour {
 public string[] playerNames={"JOGADOR 01","JOGADOR 02","JOGADOR 03","JOGADOR 04","JOGADOR 05","JOGADOR 06","JOGADOR 07","JOGADOR 08"};
 public int[] wins={125,118,111,104,97,90,83,76},elo={2002,1911,1820,1729,1638,1547,1456,1365},coins={9800,9160,8520,7880,7240,6600,5960,5320};
 public TMPro.TMP_Text[] winsRows,eloRows,coinsRows;
 void Start(){RefreshBoards();}
 public void RefreshBoards(){for(int i=0;i<playerNames.Length;i++){string prefix="#"+(i+1)+"   "+playerNames[i]+"   ";if(i<winsRows.Length&&i<wins.Length)winsRows[i].text=prefix+wins[i];if(i<eloRows.Length&&i<elo.Length)eloRows[i].text=prefix+elo[i];if(i<coinsRows.Length&&i<coins.Length)coinsRows[i].text=prefix+coins[i];}}
}

