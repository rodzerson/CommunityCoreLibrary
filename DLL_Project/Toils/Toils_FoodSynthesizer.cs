﻿using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using CommunityCoreLibrary;
using RimWorld;
using Verse;
using Verse.AI;

namespace CommunityCoreLibrary
{

    public static class Toils_FoodSynthesizer
    {
        
        public static Toil TakeFromSynthesier( TargetIndex ind, Pawn taker )
        {
            var synthesizer = (Building_AutomatedFactory) taker.jobs.curJob.GetTarget( ind ).Thing;
            var takeFromSynthesizer = new Toil();
            //Log.Message( string.Format( "{0}.TakeMealFromSynthesizier( {1}, {2} )", eater == null ? "null" : eater.NameStringShort, synthesizer == null ? "null" : synthesizer.ThingID, bestDef == null ? "null" : bestDef.defName ) );
            if( !synthesizer.IsReservedBy( taker ) )
            {
                takeFromSynthesizer.defaultCompleteMode = ToilCompleteMode.Delay;
                takeFromSynthesizer.AddEndCondition( () =>
                {
                    if( taker.Map.reservationManager.ReservedBy( synthesizer, taker ) )
                    {
                        taker.Map.reservationManager.Release( synthesizer, taker );
                    }
                    return JobCondition.Incompletable;
                } );
                takeFromSynthesizer.defaultDuration = Building_AutomatedFactory.ERROR_PRODUCTION_TICKS;
            }
            else
            {
                takeFromSynthesizer.defaultCompleteMode = ToilCompleteMode.Delay;
                takeFromSynthesizer.defaultDuration = synthesizer.ReservedProductionTicks;
                takeFromSynthesizer.AddFinishAction( () =>
                {
                    var thingToTake = synthesizer.TryProduceAndReleaseFor( taker, true );
                    if( thingToTake == null )
                    {   // This should never happen, why is it?
                        Log.Error( string.Format( "'{0}' is unable to take from '{1}'", taker.NameStringShort, synthesizer.ThingID ) );
                        taker.jobs.curDriver.EndJobWith( JobCondition.Incompletable );
                    }
                    else
                    {
                        taker.carryTracker.TryStartCarry( thingToTake );
                        taker.jobs.curJob.SetTarget( ind, taker.carryTracker.CarriedThing );
                    }
                } );
            }
            return takeFromSynthesizer;
        }

        // TODO:  The following two methods are 99% obsoleted

        /*
        
        public static Toil TakeDrugFromSynthesizer( TargetIndex ind, Pawn eater )
        {
            //would like to put this in the FoodSynthesis class but this would require changing the allowed function
            //signature to take pawn as well. And a full refactor of all of the functions.
            Func<ThingDef, bool> validator = FoodSynthesis.IsDrug;
            var synthesizer = (Building_AutomatedFactory) eater.jobs.curJob.GetTarget( ind ).Thing;
            ThingDef thingToGet = eater.jobs.curJob.plantDefToSow;
            if (synthesizer == null)
            {
                throw new Exception("Non Factory object passed to TakeDrugFromSynthesizer");
            }
            if (eater.MentalState is MentalState_BingingDrug)
            {
                var bingingChemical = ((MentalState_BingingDrug) eater.MentalState).chemical;
                //Pawn is binging, will only take the drug that satisfies need.
                validator = thingDef =>
                {
                    if (thingDef.HasComp(typeof(CompProperties_Drug)))
                    {
                        var drugComp = (CompProperties_Drug) thingDef.GetCompProperty(typeof(CompProperties_Drug));
                        return drugComp.chemical == bingingChemical;
                    }
                    return false;
                };
            }
            else
            {
                if (thingToGet != null)
                {
                    //ThingToProduce was set during the social relax search. This is the drug the pawn ended up with.
                    //Already checked for drug policy.
                    validator = thingDef =>
                    {
                        return thingDef == thingToGet;
                    };
                }
                else
                {
                    if (eater.drugs != null)
                    {
                        var drugPolicy = eater.drugs;
                        //Building_AutomatedFactory was passed with no product information OR ThingToProduce == null.
                        //Find best within drug policy
                        validator = thingDef =>
                        {
                            return eater.drugs.AllowedToTakeScheduledNow(thingDef);
                        };
                    }
                }
            }
            return TakeFromSynthesier( ind, eater, validator, FoodSynthesis.SortDrug);
        }

        public static Toil TakeMealFromSynthesizer( TargetIndex ind, Pawn eater)
        {
            var synthesizer = (Building_AutomatedFactory) eater.jobs.curJob.GetTarget( ind ).Thing;
            Building_AutomatedFactory factory;
            Thing target = eater.jobs.curJob.GetTarget(ind).Thing;
            FactoryWithProduct factoryWithProduct = target as FactoryWithProduct;
            if (factoryWithProduct == null)
            {
                factory = target as Building_AutomatedFactory;
            }
            else
            {
                factory = factoryWithProduct.Factory;
            }
            if (factory == null)
            {
                throw new Exception("Non Factory object passed to TakeMealFromSynthesizer");
            }
            //TODO modify validator to take specific meal if factoryWithProduct.ThingToProduce != null
            return TakeFromSynthesier( ind, eater, FoodSynthesis.IsMeal, FoodSynthesis.SortMeal);
        }

        */

    }

}
