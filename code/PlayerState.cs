using System;

namespace Jinroo;

public class PlayerState : Component
{
  [HostSync] public int PlayerIndex { get; set; } = -1;

  [HostSync] public string Name { get; set } = "";

  GameObject PlayerPawn;

  public static PlayerState Create( string name = null )
  {
    var holder = new GameObject();
    var ps = holder.Components.Create<PlayerState>();
    ps.Name = name;
    holder.Name = $"PlayerState {name}";
    holder.NetworkSpawn();

    return ps;
  }

  [Broadcast( NetPermission.HostOnly )]
  public void Multicast_SetPlayerPawn( int playerIndex, Guid playerPawnId )
  {

    PlayerPawn = Scene.Directory.FindByGuid( playerPawnId );
    GameplayStatics.__SetPlayerPawn( playerIndex, PlayerPawn );

    // Update the nameplate 
    var nameplate = PlayerPawn.Components.GetAll<Nameplate>( FindMode.EverythingInDescendants ).FirstOrDefault();

    if ( nameplate is not null )
    {
      nameplate.Name = Name;
      nameplate.PlayerIndex = playerIndex;
    }
  }
}