using System;
using System.Threading.Tasks;

namespace Jinroo;

public abstract class ARole
{
  public RoleTeam Team { get; set; }

  public RoleType Type { get; set; }

  public GameMode GameMode { get; set; }

  public Player Player;

  public int Turn
  {
    get => GameMode.Turn;
  }

  public void CheckWin() => GameMode.RequestCheckWin();

  public abstract string GetKey();

  public abstract string GetName();

  public virtual string GetDescription() => "";

  public virtual string GetTaskText() => "";

  public virtual int GetOrder() => 0;

  public virtual string GetBroadcastTaskText() => "";

  public virtual string GetIcon() => "/textures/roles/card_role_unknown.svg";

  public virtual bool IsUnique() => true;

  public virtual int GetTimeout() => 30;

  public virtual string GetWinConditionTest()
  {
    if ( Team == RoleTeam.WEREWOLVES )
      return "Win with the werewolves when there are no villagers alive.";
    else if ( Team == RoleTeam.VILLAGE )
      return "Win with the village.";

    return "";
  }

  public virtual string GetAbilityText() => "";



  // Event callback
  public virtual async Task OnBeginNight( TaskSource task ) { }

  public virtual async Task OnNightTurn( TaskSource task ) { }

  public virtual async Task OnEndNight( TaskSource task ) { }

  public virtual async Task OnBeginDay( TaskSource task ) { }

  public virtual async Task OnEndDay( TaskSource task ) { }

  public virtual async Task OnPlayerDead( TaskSource task, Player victim ) { }

  public void OnCheckWin( ref RoleTeam winnerTeam ) { }

  public void OnEndGame( RoleTeam winnerTeam, ref List<Player> winners ) { }


  public virtual async Task Client_OnTask( PlayerController controller, Dictionary<string, object> data, TaskSource task ) { }

  public virtual async Task Client_OnTaskRequest( PlayerController controller, Dictionary<string, object> requestData, TaskSource task, Guid guid )
  {

  }


  public virtual void OnExperiment() { }

  // OnCheckWin
  // OnEndGame
  // WinnerTeam
  // 


  private static Dictionary<string, ARole> RegisteredRoles = new();


  private bool InWaitingResponse = false;
  private float TimeoutTime = 0;

  private Dictionary<string, object> TaskResponseData;

  private Guid TokenRequest;


  protected bool IsWaitingResponseActive()
  {
    if ( InWaitingResponse && TimeoutTime == 0 )
      return true;

    if ( InWaitingResponse && TimeoutTime > 0 && RealTime.Now >= TimeoutTime )
      return false;



    return InWaitingResponse;
  }

  virtual protected async Task<Dictionary<string, object>> RequestTask( TaskSource task, Dictionary<string, object> data, float timeoutTime = 0 )
  {
    InWaitingResponse = true;
    TimeoutTime = timeoutTime;
    TaskResponseData = null;

    // generate guid
    TokenRequest = Guid.NewGuid();

    Player?.Controller?.Client_OnTaskRequest( data, TokenRequest );

    while ( IsWaitingResponseActive() )
    {

      await task.Delay( 1000 / 30 );
    }

    if ( TaskResponseData is null )
      TaskResponseData = new();

    return TaskResponseData;
  }

  virtual public void OnTaskResponse( Dictionary<string, object> responseData, Guid token )
  {
    if ( token == Guid.Empty || token != TokenRequest || !InWaitingResponse )
      return;

    TaskResponseData = responseData;
    InWaitingResponse = false;
    TokenRequest = Guid.Empty;
  }

  public static void RegisterRole( ARole role )
  {
    if ( !RegisteredRoles.ContainsKey( role.GetKey() ) )
    {
      RegisteredRoles.Add( role.GetKey(), role );
    }
  }
}