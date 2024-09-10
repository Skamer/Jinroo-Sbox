using System;
using System.Threading.Tasks;

namespace Jinroo;

public class WereWolfRole : ARole
{
  static bool WasExecuted = false;

  public WereWolfRole()
  {
    Team = RoleTeam.WEREWOLVES;
    Type = RoleType.WEREWOLF;
  }

  public const string Key = "werewolf";

  public override string GetKey() => Key;


  public override int GetOrder() => (int)RoleOrder.WEREWOLF;

  public override bool IsUnique() => false;

  public override string GetName() => "Werewolf";

  public override string GetIcon() => "/textures/roles/card_role_werewolf.svg";

  public override string GetAbilityText()
  {
    return "Each night, the werewolf to vote for someone which will be killed.";
  }

  public override async Task OnBeginNight( TaskSource task )
  {
    WasExecuted = false;
  }


  public override async Task OnNightTurn( TaskSource task )
  {
    if ( WasExecuted )
      return;

    WasExecuted = true;

    List<Player> werewolves;

    try
    {
      werewolves = GameMode.Players.Where( player => player.IsAlive && player.Role?.Type == RoleType.WEREWOLF ).ToList();
    }
    catch ( Exception ex )
    {
      werewolves = new();
    }

    if ( werewolves.Count == 0 )
      return;

    List<Player> choices;

    try
    {
      choices = GameMode.Players.Where( player => player.IsAlive ).ToList();
    }
    catch
    {
      choices = new();
    }


    Dictionary<int, RoleType> rolesType = null;
    if ( Turn == 1 )
    {
      rolesType = new();
      werewolves.ForEach( ( player ) => rolesType.Add( player.index, RoleType.WEREWOLF ) );
    }


    foreach ( var werewolf in werewolves )
    {
      if ( Turn == 1 && rolesType is not null && werewolf.Controller is not null )
        werewolf.Controller.Client_SetRoleType( rolesType );

      werewolf.GiveVision();
    }

    GameMode.JoinChat( werewolves );

    GameMode.GameState.Multicast_SendServerMessage( "The werewolves are deciding which victim to eat." );
    GameMode.GameState.Multicast_SetPhase( GamePhaseType.WEREWOLVES, GetTimeout() );


    var results = await GameMode.WaitingVote( werewolves, choices, GetTimeout(), 15 );


    foreach ( var werewolf in werewolves )
      werewolf.RestrictVision();

    GameMode.LeaveChat( werewolves );

    if ( results.Count != 1 )
    {
      GameMode.BroadcastServerMessage( werewolves, "Tie in the votes ! Therefore no one will be killed" );
      return;
    }

    results.First().TryKill( KillReason.WEREWOLF );
  }


  // void OnTask()
  // {

  //   var werewolves = GameMode.Players.Where( player => player.Role?.Type == RoleType.WEREWOLF ).ToList();

  //   // GameplayStatics.GetVote()

  //   // var task = GameTask.DelaySeconds( 4 );
  //   Log.Warning( "Task Before Vote" );
  //   // Log.Warning( task.IsCompleted );

  //   // task.ContinueWith( task => Log.Info( $"FInished Task {!task.IsCompleted}" ) );

  //   // Log.Warning( $"Task After Vote {!task.IsCompleted}" );

  //   // // getTask = Task.Delay

  //   var vote = new Vote( GameMode, werewolves, GameMode.Players );
  //   vote.Start();
  // }


}