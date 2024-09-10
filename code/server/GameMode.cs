using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;


namespace Jinroo;


public struct GameNetworkProps
{
  public int index;

  public Guid connectionId;

  public Guid controllerId;

  public Guid stateId;
}

public class GameMode : Component
{
  public Vector3 SpawnOriginPosition { get; set; } = Vector3.Zero;

  public GameLobby Lobby;

  public GameState GameState;

  public List<ARole> RemainingRoles = new();

  // public Dictionary<int, ARole> PlayerRoles = new();

  public List<ARole> Roles = new();

  public ARole CurrentRoleInProcess;

  public List<Player> Players = new();

  public List<Player> PendingKills = new();

  public List<Player> PreviousKills = new();

  public bool IsPlaying = false;

  public int Turn = 0;

  // public GameMode( GameLobby lobby, TaskSource task )
  // {
  //   Lobby = lobby;
  //   Task = task;
  // }

  public static GameMode Create( GameLobby lobby )
  {
    var holder = new GameObject();
    holder.Name = "GameMode";

    var gm = holder.Components.Create<GameMode>();
    gm.Lobby = lobby;

    return gm;
  }

  public void Start( List<Connection> participants, Dictionary<string, int> RoleSettingAmount, int maxBotsAmount = 0 )
  {
    Players.Clear();
    RemainingRoles.Clear();

    foreach ( KeyValuePair<string, int> pair in RoleSettingAmount )
    {
      string roleKey = pair.Key;
      int amount = pair.Value;

      for ( int i = 0; i < amount; i++ )
      {
        var role = GameplayStatics.CreateRole( roleKey );

        if ( role is not null )
        {
          role.GameMode = this;
          RemainingRoles.Add( role );
        }
      }
    }

    // Create the GameState 
    GameState = GameState.Create();

    List<GameNetworkProps> networkPropsList = new();

    var playerIndex = 0;
    foreach ( var participant in participants )
    {
      // Create the PlayerState
      var playerState = PlayerState.Create( participant.DisplayName );
      playerState.PlayerIndex = playerIndex;
      GameState.Multicast_AddPlayerState( playerState.GameObject.Id );

      // Create the Player Controller
      var playerController = PlayerController.Create( participant );
      GameplayStatics.__SetPlayerController( playerIndex, playerController );

      var player = new Player( playerIndex, this, GameState, playerState, playerController );
      player.Connection = participant;
      Players.Add( player );


      GameNetworkProps networkProps;
      networkProps.index = playerIndex;
      networkProps.stateId = playerState.GameObject.Id;
      networkProps.connectionId = participant.Id;
      networkProps.controllerId = playerController.GameObject.Id;

      networkPropsList.Add( networkProps );

      playerIndex++;
    }

    // Only for debug
    if ( maxBotsAmount > 0 )
    {
      int botsToAdd = Math.Clamp( maxBotsAmount, 0, 16 - participants.Count() );
      for ( var i = 0; i < botsToAdd; i++ )
      {


        // Create the PlayerState
        var playerState = PlayerState.Create( $"(Bot) {GetBotName( i )}" );
        playerState.PlayerIndex = playerIndex;
        GameState.Multicast_AddPlayerState( playerState.GameObject.Id );

        // Note: A bot has no controller, need checking ref to player controller in the gameplay for avoiding
        // exceptions.
        var player = new Player( playerIndex, this, GameState, playerState );
        Players.Add( player );

        GameNetworkProps networkProps = new();
        networkProps.index = playerIndex;
        networkProps.stateId = playerState.GameObject.Id;

        networkPropsList.Add( networkProps );

        playerIndex++;
      }
    }

    Lobby.Multicast_SetGameNetworkProps( networkPropsList );

    _ = BeginPlay();
  }

  public async Task BeginPlay()
  {
    IsPlaying = true;

    SpawnPlayers();

    AssignRoles();

    GameState.Multicast_SetPhase( GamePhaseType.STARTUP, 10 );
    GameState.Multicast_SetDayTime( GameDayTime.NIGHT );
    RestrictVisionForAllPlayers();
    GameState.Multicast_UpdateRoleComposition( GetRolesComposition() );

    await Task.Delay( 10 * 1000 );

    while ( IsPlaying )
    {
      await BeginTurn();
      await Task.Delay( 1000 / 30 );
    }
  }

  public async Task BeginTurn()
  {
    Turn++;

    GameState.Multicast_SendServerMessage( $"Turn {Turn}", ServerMessageType.TURN );

    // ---------- Night Start ---------------

    GameState.Multicast_SetPhase( GamePhaseType.DUSK, 40 );
    GameState.Multicast_SetDayTime( GameDayTime.NIGHT );

    foreach ( var player in Players )
    {
      player.RestrictVision();
      if ( player.IsAlive )
      {
        player.LeaveChat();
      }
    }

    GameState.Multicast_OnBeginNight();

    await RunRolesPhaseTask( "OnBeginNight" );

    await RunRolesPhaseTask( "OnNightTurn" );

    await RunRolesPhaseTask( "OnEndNight" );

    // ------- Day Start -------------------------

    // Give the vision and players alive will join the global chat.
    foreach ( var player in Players )
    {
      player.GiveVision();
      if ( player.IsAlive )
      {
        player.JoinChat();
      }
    }

    GameState.Multicast_OnBeginDay();
    GameState.Multicast_SetDayTime( GameDayTime.DAY );

    await ProcessPendingKills();

    await RunRolesPhaseTask( "OnBeginDay" );

    if ( CheckWin( true ) )
      return;

    if ( PreviousKills.Count == 0 )
      GameState.Multicast_SendServerMessage( "Strange ! No one has died." );


    // ------------- VOTE VILLAGE --------------

    // Start the vote for the village decide to chose the player to eliminated. 
    float voteTimeout = 90;
    float minVoteTimeout = 10;

    GameState.Multicast_SetPhase( GamePhaseType.VILLAGE_VOTE, voteTimeout );

    List<Player> alivePlayers;

    try
    {
      alivePlayers = Players.Where( player => player.IsAlive ).ToList();
    }
    catch ( Exception ex )
    {
      alivePlayers = new();
    }



    var voteResults = await WaitingVote( alivePlayers, alivePlayers, voteTimeout, minVoteTimeout );

    if ( voteResults.Count == 1 )
    {
      voteResults.First().TryKill( KillReason.VILLAGE );
    }
    else
    {
      GameState.Multicast_SendServerMessage( "Tie Vote ! No one will be executed." );
    }

    await ProcessPendingKills();
    // ------------ END DAY -------------------
    await RunRolesPhaseTask( "OnEndDay" );

    if ( CheckWin( true ) )
      return;
  }

  public async Task RunRolesPhaseTask( string phase )
  {
    foreach ( var role in Roles )
    {
      CurrentRoleInProcess = role;

      if ( phase == "OnBeginNight" )
        await role.OnBeginNight( Task );
      else if ( phase == "OnNightTurn" )
        await role.OnNightTurn( Task );
      else if ( phase == "OnEndNight" )
        await role.OnEndNight( Task );
      else if ( phase == "OnBeginDay" )
        await role.OnBeginDay( Task );
      else if ( phase == "OnEndDay" )
        await role.OnEndDay( Task );


      CurrentRoleInProcess = null;
    }
  }

  // public async Task ProcessPendingKill()
  // {
  //   foreach ( var player in PendingKills )
  //   {
  //     player.Kill();

  //     foreach ( var role in Roles )
  //     {
  //       CurrentRoleInProcess = role;
  //       await role.OnPlayerDead( Task, player );
  //       CurrentRoleInProcess = null;
  //     }
  //   }

  //   PendingKills.Clear();
  // }

  private async Task __ProcessPendingKills()
  {
    var pendingKillsCopy = new List<Player>( PendingKills );

    foreach ( var player in pendingKillsCopy )
    {
      if ( PendingKills.Contains( player ) )
      {
        player.Kill();

        foreach ( var role in Roles )
        {
          CurrentRoleInProcess = role;
          await role.OnPlayerDead( Task, player );
          CurrentRoleInProcess = null;
        }

        PreviousKills.Add( player );
      }
    }

    foreach ( var processedPlayer in pendingKillsCopy )
    {
      PendingKills.Remove( processedPlayer );
    }
  }

  public async Task ProcessPendingKills()
  {
    PreviousKills.Clear();

    while ( PendingKills.Count > 0 )
      await __ProcessPendingKills();


    // Update the ROles Count if there is atleast a dead
    if ( PreviousKills.Count > 0 )
      GameState.Multicast_UpdateRoleComposition( GetRolesComposition() );
  }

  public void GiveVisionForAllPlayers()
  {
    foreach ( var player in Players )
      player.GiveVision();
  }

  public void RestrictVisionForAllPlayers()
  {
    foreach ( var player in Players )
      player.RestrictVision();
  }

  private bool CheckWinRequested = false;
  public bool CheckWin( bool force = false )
  {
    if ( !force && !CheckWinRequested )
      return false;

    Dictionary<RoleTeam, int> teamCount = new();

    foreach ( var player in Players )
    {
      if ( !player.IsAlive )
        continue;

      var team = player.Role.Team;
      if ( team != RoleTeam.NEUTRAL && team != RoleTeam.NONE )
      {
        if ( teamCount.ContainsKey( team ) )
        {
          teamCount[team]++;
        }
        else
        {
          teamCount[team] = 1;
        }
      }
    }

    var winnerTeam = RoleTeam.NONE;
    var teamCountAlive = teamCount.Count;
    if ( teamCountAlive == 1 )
    {
      winnerTeam = teamCount.Keys.First();
    }


    foreach ( var role in Roles )
    {
      role.OnCheckWin( ref winnerTeam );
    }

    if ( teamCountAlive == 0 || winnerTeam != RoleTeam.NONE )
    {
      EndGame( winnerTeam );

      return true;
    }

    CheckWinRequested = false;

    return false;
  }

  public void RequestCheckWin()
  {
    CheckWinRequested = true;
  }

  public void EndGame( RoleTeam winnerTeam )
  {
    var winners = Players.Where( player => player.Role.Team == winnerTeam ).ToList();

    foreach ( var role in Roles )
    {
      role.OnEndGame( winnerTeam, ref winners );
    }


    List<int> winnersIndex = null;
    try
    {
      winnersIndex = winners.Select( player => player.index ).ToList();
    }
    catch
    {
      winnersIndex = new();
    }

    Dictionary<int, string> playerRoles = null;
    try
    {
      playerRoles = Players.ToDictionary( player => player.index, player => player.Role?.GetKey() );
    }
    catch
    {
      playerRoles = new();
    }



    string winnerText = "";
    try
    {
      string playersListText = string.Join( ", ", winners.Where( player => player.Role.Team == winnerTeam && player.IsAlive ).Select( player => $"{player.State.Name} ({player.Role.GetName()}) " ) );

      winnerText = $"Congratulations to {playersListText} for enabling his team to win.";
    }
    catch ( Exception e ) { }


    GameState.Multicast_OnEndGame( winnerTeam, winnersIndex, playerRoles, winnerText );

    IsPlaying = false;
  }

  public Dictionary<string, int> GetRolesComposition()
  {
    Dictionary<string, int> RolesAmount = new();

    foreach ( var player in Players )
    {
      if ( !player.IsAlive )
        continue;

      var roleKey = player.Role?.GetKey();
      if ( string.IsNullOrEmpty( roleKey ) )
        continue;

      if ( RolesAmount.ContainsKey( roleKey ) )
      {
        RolesAmount[roleKey]++;
      }
      else
      {
        RolesAmount[roleKey] = 1;
      }
    }

    return RolesAmount;
  }


  // public void StartNight()
  // {
  //   // Log.Warning( "-------------- GUID" );
  //   // Log.Error( Guid.NewGuid() );
  //   // Log.Error( Guid.NewGuid() );
  //   // Log.Error( Guid.NewGuid() );
  //   // Log.Error( Guid.NewGuid() );
  //   // Log.Error( Guid.NewGuid() );
  //   // Log.Warning( "-------------- GUID" );

  //   // var taskTokenId = Guid.NewGuid();
  //   foreach ( var role in Roles )
  //   {
  //     role.OnPreNight();
  //   }


  //   // Log.Warning( "Call Taskssss" );
  //   // await Task.Run( () =>
  //   // {
  //   //   foreach ( var role in Roles )
  //   //   {
  //   //     role.OnNightTurn();
  //   //   }
  //   // } );

  //   // Task.Run()

  //   //
  //   // Log.Warning( "Runn" );
  //   // foreach ( var role in Roles )
  //   // {
  //   //   Log.Warning( $"Role: {role.GetName()} " );
  //   //   await role.OnNightTurn();
  //   // }

  //   foreach ( var role in Roles )
  //   {
  //     role.OnPostNight();
  //   }
  // }


  // public void StartDay()
  // {
  //   foreach ( var role in Roles )
  //   {
  //     role.OnPreDay();
  //   }

  //   // Get the player death and kill them 

  //   foreach ( var role in Roles )
  //   {
  //     role.OnPostDay();
  //   }
  // }

  // Spawn Players 
  public void SpawnPlayers()
  {
    var playerCount = Players.Count();
    float radius = 300f;

    foreach ( var player in Players )
    {
      float angle = player.index * MathF.PI * 2 / playerCount;

      float x = MathF.Cos( angle ) * radius;
      float y = MathF.Sin( angle ) * radius;

      Vector3 spawnPos = SpawnOriginPosition + new Vector3( x, y, SpawnOriginPosition.y );

      var playerPawn = Lobby.PlayerPawnPrefab.Clone( spawnPos );

      if ( player.Connection is not null )
      {
        var bodyRenderer = playerPawn.Components.Get<SkinnedModelRenderer>( FindMode.InChildren );
        if ( bodyRenderer is not null )
        {
          var clothing = new ClothingContainer();
          clothing.Deserialize( player.Connection.GetUserData( "avatar" ) );
          clothing.Apply( bodyRenderer );
        }
      }

      playerPawn.NetworkSpawn();

      // Rotate the pawn toward the origin 
      playerPawn.Transform.Rotation = Rotation.LookAt( SpawnOriginPosition - spawnPos, Vector3.Up );

      player.State.Multicast_SetPlayerPawn( player.index, playerPawn.Id );
      // player.Controller.Client_Init();

      // As the bot have no controller, we need to check it
      if ( player.Controller is not null )
      {
        player.Controller.OwningPawn = playerPawn;
        player.Controller.Client_PossessPawn( playerPawn.Id );
      }
    }
  }

  public void AssignRoles()
  {
    Random random = new Random();

    foreach ( var player in Players )
    {
      int indexRole = random.Next( RemainingRoles.Count );
      var role = RemainingRoles[indexRole];

      player.AssignRole( role );

      Roles.Add( role );

      RemainingRoles.RemoveAt( indexRole );
    }

    SortRoles();
  }

  public void SortRoles()
  {
    Roles.Sort( ( a, b ) => a.GetOrder().CompareTo( b.GetOrder() ) );
  }


  // public void AssignRole( int playerIndex, ARole role )
  // {
  //   PlayerRoles[playerIndex] = role;

  //   var playerController = GameplayStatics.GetPlayerController( playerIndex );
  //   playerController.Client_AssignRole( role.GetName() );
  // }


  // VOTE System 

  // The particpants to vote
  public List<Player> VoteParticipants;
  // The players eligible to be voted
  public List<Player> VoteChoices;
  // A player list has already vote once?
  public List<Player> AlreadyVoted = new();

  public float VoteTimeoutTime;
  public bool IsWaitingVote = false;

  private float VoteStartingTime;

  private float VoteTimeout;

  private float VoteMinTimeout;

  public bool IsWaitingVoteActive()
  {
    if ( IsWaitingVote && RealTime.Now >= VoteTimeoutTime )
      return false;

    return IsWaitingVote;
  }

  // participants -> the list of participants for vote
  // choices -> the players are eligible to be voted 
  // timeout -> how many second the timeout will be finished 
  // minTimeout -> In case where everyone has voted once, the timeout change for this value. In case where min timeout is <= 0, the timeout won't changed. 
  public async Task<List<Player>> WaitingVote( List<Player> participants, List<Player> choices, float timeout, float minTimeout )
  {
    VoteParticipants = participants;
    VoteChoices = choices;
    AlreadyVoted.Clear();

    // Time settings 
    VoteStartingTime = RealTime.Now;
    VoteTimeout = timeout;
    VoteMinTimeout = minTimeout;
    VoteTimeoutTime = VoteStartingTime + timeout;

    IsWaitingVote = true;


    var p = participants.Select( player => player.index ).ToList();
    var c = choices.Select( player => player.index ).ToList();
    foreach ( var participant in participants )
    {
      participant.Controller?.Client_OnStartVote( p, c, VoteTimeoutTime );
    }

    while ( IsWaitingVoteActive() )
    {
      await Task.Delay( 1000 / 30 );
    }

    IsWaitingVote = false;

    Dictionary<Player, int> voteCounts = new();
    foreach ( var participant in participants )
    {
      var votedFor = participant.VoteResult;
      if ( votedFor is null )
        continue;

      if ( voteCounts.ContainsKey( votedFor ) )
        voteCounts[votedFor]++;
      else
        voteCounts[votedFor] = 1;
    }

    List<Player> results;

    try
    {
      int maxVotes = voteCounts.Max( kv => kv.Value );
      results = voteCounts.Where( kv => kv.Value == maxVotes ).Select( kv => kv.Key ).ToList();
    }
    catch ( Exception ex )
    {
      results = new();
    }

    foreach ( var participant in participants )
    {
      participant.Controller?.Client_OnEndVote();
      participant.VoteResult = null;
    }

    return results;
  }

  public List<int> GetVotingPLayersIndex( Player target )
  {
    List<int> indexes = new();
    foreach ( var participant in VoteParticipants )
    {
      if ( participant.VoteResult == target )
        indexes.Add( participant.index );
    }

    return indexes;
  }

  public void OnPlayerVote( Player instigator, Player vote, Player previousVote )
  {
    // Notify the clients when there are vote has been changed on the player.
    if ( vote != previousVote )
    {
      // OnPlayerVoteRecivedChanged()
      if ( previousVote is not null )
      {
        var votingPlayer = GetVotingPLayersIndex( previousVote );
        foreach ( var participant in VoteParticipants )
        {
          participant.Controller?.Client_OnPlayerReceivedVoteChanged( previousVote.index, votingPlayer );
        }
      }

      if ( vote is not null )
      {
        var votingPlayer = GetVotingPLayersIndex( vote );
        foreach ( var participant in VoteParticipants )
        {
          participant.Controller?.Client_OnPlayerReceivedVoteChanged( vote.index, votingPlayer );
        }
      }

      // If the player had a vote, has changed its vote for 
      // if the player have not a vote, 
      if ( previousVote is null && vote is not null )
      {
        BroadcastServerMessage( VoteParticipants, $"{instigator.State.Name} has voted for {vote.State.Name}", ServerMessageType.VOTE );
      }
      else if ( previousVote is not null && vote is null )
      {
        BroadcastServerMessage( VoteParticipants, $"{instigator.State.Name} withdrew his vote from {previousVote.State.Name}", ServerMessageType.VOTE );
      }
      else if ( previousVote is not null && vote is not null )
      {
        BroadcastServerMessage( VoteParticipants, $"{instigator.State.Name} changed his vote to {vote.State.Name}", ServerMessageType.VOTE );
      }
    }

    if ( AlreadyVoted.Contains( instigator ) && AlreadyVoted.Count >= VoteParticipants.Count )
      return;

    AlreadyVoted.Add( instigator );

    if ( AlreadyVoted.Count < VoteParticipants.Count )
      return;

    if ( VoteMinTimeout <= 0 )
      return;

    var newTimeoutTime = Math.Min( VoteTimeoutTime, RealTime.Now + VoteMinTimeout );
    if ( newTimeoutTime != VoteTimeoutTime )
    {
      // Client.VoteNewTimeOut
      GameState.Multicast_SetPhaseTimeout( VoteMinTimeout );
    }

    VoteTimeoutTime = newTimeoutTime;
  }

  // Chat System 
  Dictionary<GameChannels, List<Player>> ChatChannels = new() { { GameChannels.GLOBAL, new() } };

  public void JoinChat( Player player, GameChannels channel = GameChannels.GLOBAL )
  {
    List<Player> playersInChannel;

    if ( ChatChannels.ContainsKey( channel ) )
    {
      playersInChannel = ChatChannels[channel];
    }
    else
    {
      playersInChannel = new();
      ChatChannels[channel] = playersInChannel;
    }

    if ( playersInChannel.Contains( player ) )
      return;

    playersInChannel.Add( player );
  }

  public void JoinChat( List<Player> players, GameChannels channel = GameChannels.GLOBAL, bool ignoreDeadPlayers = true )
  {
    if ( players is null || players.Count == 0 )
      return;

    foreach ( var player in players )
    {
      if ( ignoreDeadPlayers && !player.IsAlive )
        continue;

      JoinChat( player, channel );
    }
  }

  public List<Player> GetPlayersInChatChannel( GameChannels channel = GameChannels.GLOBAL )
  {
    return ChatChannels.GetValueOrDefault( channel, new() );
  }

  public void LeaveChat( Player player, GameChannels channel = GameChannels.GLOBAL )
  {
    var playersInChannel = ChatChannels[channel];

    if ( playersInChannel is null )
      return;

    playersInChannel.Remove( player );
  }

  public void LeaveChat( List<Player> players, GameChannels channel = GameChannels.GLOBAL, bool ignoreDeadPlayers = true )
  {
    if ( players is null || players.Count == 0 )
      return;

    foreach ( var player in players )
    {
      if ( ignoreDeadPlayers && !player.IsAlive )
        continue;

      LeaveChat( player, channel );
    }
  }

  public void BroadcastServerMessage( List<Player> players, string message, ServerMessageType type = ServerMessageType.INFO, GameChannels channel = GameChannels.GLOBAL )
  {
    if ( players is null || players.Count == 0 )
      return;

    foreach ( var player in players )
    {
      if ( player.Controller is null )
        continue;

      player.Controller.Client_SendServerMessage( message, type, channel );
    }
  }

  List<string> BotNames = new()
  {
    "Francis", "Audrey", "Esperanza", "Orva", "Antoinette", "Marie", "Moore",
    "Morgana", "Clovis", "Raina", "Julien", "Xavier", "Thierry", "William",
    "Sandra", "Thomas", "Lukas", "Jessica", "Philipp", "Laura", "Johanna"
  };
  public string GetBotName( int playerIndex )
  {
    var name = BotNames[playerIndex];

    if ( name != "" )
      return name;

    return "No Name";
  }
}
