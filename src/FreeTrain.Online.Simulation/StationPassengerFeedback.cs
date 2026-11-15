// ExAV 883cf7d Station.load/unloadPassengers, LandValue.addQ and
// GlobalTrafficMonitor.NotifyPassengerTransport; LGPL-2.1-or-later.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class StationPassengerFeedback
{
    // Called only on the detached minute. No process-global event subscribers or
    // effects from a previous publication may enter this transaction.
    public static MixedConstructionState Apply(MixedConstructionState before, Clock clock,
        ImmutableArray<StationPassengerEffect> effects)
    {
        var world=before.World;
        foreach(var effect in effects)
        {
            switch(effect)
            {
                case StationPassengerEffect.LandValue land:
                    var station=world.Stations?.Building(land.StationId) ?? throw new ArgumentException("Missing feedback station.");
                    if(station.Location!=land.Location || !float.IsFinite(land.Amount) || land.Amount<0)
                        throw new ArgumentException("Invalid passenger land-value feedback.");
                    // addQ(0) has no field change. Do not activate an otherwise
                    // unused field for empty unloads or depleted boarding.
                    if(land.Amount==0) break;
                    if(world.City==null)
                    {
                        var profile=world.Profile.City ?? throw new CityConsumerUnavailableException("land-value");
                        var state=CityGraph.Empty(profile,world.Terrain.Space).State;
                        var interval=checked(60L*state.Settings.UpdateFrequencyHours);
                        var order=clock.ScheduleRepeated(CityHandlers.Heat,interval);
                        var timer=new ScheduledAction(order,order,checked(clock.Time.AbsoluteMinute+interval),CityHandlers.Heat,interval);
                        world=world.Replace(world.Revision,world.Terrain,world.Spatial.Capture(),world.Voxels,world.Slopes,
                            city:new(profile,state with {HeatCallback=timer}));
                    }
                    world=CityHeat.Deposit(world,land.Location,land.Amount);
                    break;
                case StationPassengerEffect.Transport transport:
                    if(transport.Passengers<=0 || world.Stations?.Building(transport.StationId)==null)
                        throw new ArgumentException("Invalid passenger transport feedback.");
                    // CITY-D016: the sole bundled development subscription is
                    // commented out in ExAV. There is no active transport
                    // consumer, traffic counter or bus simulation to update.
                    break;
                case StationPassengerEffect.TrainChanged:
                case StationPassengerEffect.StationChanged:
                    break; // The callback candidate already contains these changes.
                default:
                    throw new ArgumentException("Unknown passenger feedback.");
            }
        }
        return ReferenceEquals(world,before.World) ? before : new(world,before.Accounts);
    }
}
