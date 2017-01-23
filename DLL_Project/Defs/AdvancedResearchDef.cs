﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;

using CommunityCoreLibrary.Controller;

namespace CommunityCoreLibrary
{

    public class AdvancedResearchDef : Def
    {

        #region XML Data

        private const int                   InvalidPriority = 0xBADC0DE;
        public const int                    HelpConsolidatePriority = -1;

        // Processing priority so everything happens in the order it should
        // Lower value is higher priority
        public int                          Priority = InvalidPriority;

        // Flag this as true to hide/lock, false (default) for show/unlock
        public bool                         HideDefs = false;

        // Research requirement
        public List< ResearchProjectDef >   researchDefs;


        // These are optionally defined in xml

        public bool                         ConsolidateHelp;
        public HelpCategoryDef              helpCategoryDef;
        public bool                         HideUntilResearched;

        public List< RecipeDef >            recipeDefs;
        public List< string >               sowTags;
        public List< ThingDef >             thingDefs;
        public List< ResearchProjectDef >   effectedResearchDefs;
        public List< AdvancedResearchMod >  researchMods;

        #endregion

        [Unsaved]

        #region Instance Data

        ResearchEnableMode                  researchState;
        bool                                researchSorted;

        HelpDef                             helpDef;

        List< AdvancedResearchDef >         matchingAdvancedResearch;
        AdvancedResearchDef                 helpConsolidator;

        float                               totalCost = -1;

        ModHelperDef                        modHelperDef;

        #endregion

        #region Query State

        public ResearchEnableMode           ResearchState
        {
            get
            {
                return researchState;
            }
        }

        public bool                         IsHelpEnabled
        {
            get
            {
                return (
                    ( !HideUntilResearched )||
                    ( researchState != ResearchEnableMode.Incomplete )
                );
            }
        }

        private bool                        isLockedOutChecked = false;
        private bool                        isLockedOut;
        public bool                         IsLockedOut()
        {
            if( !isLockedOutChecked )
            {
                isLockedOutChecked = true;
                isLockedOut = false;
                foreach( var p in researchDefs )
                {
                    if( p.IsLockedOut() )
                    {
                        // Any of the research parents locked out?
                        isLockedOut = true;
                        break;
                    }
                }
            }
            return isLockedOut;
        }

        private bool                        isValidChecked = false;
        private bool                        isValid;
        public bool                         IsValid()
        {
            if( !isValidChecked )
            {
#if DEVELOPER
                try{
#endif
                // Hopefully...
                isValid = true;

                var ModContentPack = Find_Extensions.ModByDefOfType<AdvancedResearchDef>( defName );
                modHelperDef = Find_Extensions.ModHelperDefForMod( ModContentPack );

                if(
                    ( modHelperDef == null )||
                    ( modHelperDef.dummy )
                )
                {
                    // Missing ModHelperDef (not dummyable)
                    isValid = false;
                    CCL_Log.TraceMod(
                        this,
                        Verbosity.FatalErrors,
                        "Requires ModHelperDef"
                    );
                }

#if DEBUG
                // Validate research
                if( researchDefs.NullOrEmpty() )
                {
                    // Invalid project
                    isValid = false;
                    CCL_Log.TraceMod(
                        this,
                        Verbosity.FatalErrors,
                        "Missing researchDefs"
                    );
                }

                // Validate priority
                if( Priority < HelpConsolidatePriority )
                {
                    isValid = false;
                    if( Priority == InvalidPriority )
                    {
                        // Priority not set (or set to the default invalid value)
                        CCL_Log.TraceMod(
                            this,
                            Verbosity.FatalErrors,
                            "Priority not set"
                        );
                    }
                    else
                    {
                        // Invalid priority
                        CCL_Log.TraceMod(
                            this,
                            Verbosity.FatalErrors,
                            "Priority must be -1 or greater and unique for matching researchDefs"
                        );
                    }
                }
                else
                {
                    var matchingResearch = this.MatchingAdvancedResearch;
                    foreach( var otherDef in matchingResearch )
                    {
                        if(
                            ( otherDef != this )&&
                            ( otherDef.Priority == this.Priority )
                        )
                        {
                            // Invalid priority
                            isValid = false;
                            CCL_Log.TraceMod(
                                this,
                                Verbosity.FatalErrors,
                                "Priority must be -1 or greater and unique for matching researchDefs"
                            );
                        }
                    }
                }

                // Validate help
                if( researchDefs.Count > 1 )
                {
                    if(
                        (
                            ( Priority != HelpConsolidatePriority )&&
                            ( ConsolidateHelp )
                        )||
                        (
                            ( Priority == HelpConsolidatePriority )&&
                            ( !ConsolidateHelp )
                        )
                    )
                    {
                        // Not properly setup as the help consolidator
                        isValid = false;
                        CCL_Log.TraceMod(
                            this,
                            Verbosity.FatalErrors,
                            string.Format( "Mismatched Priority ({0}) and ConsolidateHelp ({1}) values", Priority, ConsolidateHelp )
                        );
                    }

                    // Get the help consolidator
                    if(
                        ( HelpConsolidator == null )&&
                        ( MatchingAdvancedResearch.Count > 1 )
                    )
                    {
                        // Error processing data
                        isValid = false;
                        CCL_Log.TraceMod(
                            this,
                            Verbosity.FatalErrors,
                            string.Format( "No valid help consolidator for AdvancedResearchDef {0}", defName )
                        );
                    }

                    // Make sure the help consolidator has the proper fields defined
                    if( HelpConsolidator == this )
                    {
                        if( label.NullOrEmpty() )
                        {
                            // Error processing data
                            isValid = false;
                            CCL_Log.TraceMod(
                                this,
                                Verbosity.FatalErrors,
                                "Help Consolidator requires missing label"
                            );
                        }
                        if( description.NullOrEmpty() )
                        {
                            // Error processing data
                            isValid = false;
                            CCL_Log.TraceMod(
                                this,
                                Verbosity.FatalErrors,
                                "Help Consolidator requires missing description"
                            );
                        }
                    }
                }
                else if(
                    ( Priority == HelpConsolidatePriority )&&
                    ( ConsolidateHelp )
                )
                {
                    // Not valid setup as the help consolidator
                    isValid = false;
                    CCL_Log.TraceMod(
                        this,
                        Verbosity.FatalErrors,
                        string.Format( "Defs with a single researchDef cannot be help consolidators.  The information for this def will be added to the help for the single research prerequisite, Priority = '{0}', ConsolidateHelp = '{1}'", Priority, ConsolidateHelp )
                    );
                }

                // Validate recipes
                if( IsRecipeToggle )
                {
                    // v0.12.7 - Obsoleted check to allow for automated machines
                    // Make sure thingDefs are of the appropriate type
                    /*
                    foreach( var thingDef in thingDefs )
                    {
                        if( thingDef.thingClass.GetInterface( "IBillGiver" ) == null )
                        {
                            // Invalid project
                            isValid = false;
                            CCL_Log.AppendTrace(
                                ref stringBuilder,
                                this,
                                Verbosity.FatalErrors,
                                "ThingDef '" + thingDef.defName + "' does not implement IBillGiver"
                            );
                        }
                    }
                    */
                }

                // Validate plant sowTags
                if( IsPlantToggle )
                {
                    // Make sure things are of the appropriate class (Plant)
                    foreach( var thingDef in thingDefs )
                    {
                        if( !typeof( Plant ).IsAssignableFrom( thingDef.thingClass ) )
                        {
                            // Invalid plant
                            isValid = false;
                            CCL_Log.TraceMod(
                                this,
                                Verbosity.FatalErrors,
                                "ThingDef '" + thingDef.defName + "' ThingClass is not Plant based"
                            );
                        }
                    }

                    // Make sure sowTags are valid (!null or empty)
                    for( int i = 0; i < sowTags.Count; i++ )
                    {
                        var sowTag = sowTags[ i ];
                        if( sowTag.NullOrEmpty() )
                        {
                            isValid = false;
                            CCL_Log.TraceMod(
                                this,
                                Verbosity.FatalErrors,
                                "sowTag at index'" + i + "' is null or empty"
                            );
                        }
                    }
                }

                // Validate buildings
                if( IsBuildingToggle )
                {
                    // Make sure thingDefs are of the appropriate type (has proper designationCategory)
                    foreach( var thingDef in thingDefs )
                    {
                        if( ( thingDef.designationCategory.defName.NullOrEmpty() )||
                            ( thingDef.designationCategory.defName.ToLower() == "none" ) )
                        {
                            bool mhdUnlock = false;
                            foreach( var mhd in DefDatabase<ModHelperDef>.AllDefs )
                            {
                                if( mhd.SequencedInjectionSets != null )
                                {
                                    foreach( var tda in mhd.SequencedInjectionSets )
                                    {
                                        if(
                                            ( tda.GetType() == typeof( SequencedInjectionSet_ThingDefAvailability ) )&&
                                            ( tda.targetDefs.Contains( thingDef.defName ) )
                                        )
                                        {
                                            var injectorData = tda as SequencedInjectionSet_ThingDefAvailability;
                                            if(
                                                ( !string.IsNullOrEmpty( injectorData.designationCategory ) )&&
                                                ( injectorData.designationCategory.ToLower() != "none" )
                                            )
                                            {
                                                mhdUnlock = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                                if( mhdUnlock == true )
                                {
                                    break;
                                }
                            }
                            if( !mhdUnlock )
                            {
                                // Invalid project
                                isValid = false;
                                CCL_Log.TraceMod(
                                    this,
                                    Verbosity.FatalErrors,
                                    "ThingDef '" + thingDef.defName + "' :: designationCategory is null or empty"
                                );
                            }
                        }
                    }
                }

#endif
#if DEVELOPER
                }catch( Exception e ){
                    CCL_Log.TraceMod(
                        this,
                        Verbosity.FatalErrors,
                        string.Format( "Exception:\n{0}", e ) );
                }
#endif
            }
            return isValid;
        }

        public ResearchEnableMode           EnableMode
        {
            get
            {
                if( researchState == ResearchEnableMode.Complete )
                {
                    // Already on
                    return researchState;
                }

                // Check if research projects are done
                if( researchDefs.All( rd => rd.IsFinished ) )
                {
                    return ResearchEnableMode.Complete;
                }

                // God mode, allow it anyway
                if( DebugSettings.godMode )
                {
                    return ResearchEnableMode.GodMode;
                }

                // One or more required research is incomplete
                return ResearchEnableMode.Incomplete;
            }
        }

        public bool                         IsRecipeToggle
        {
            get
            {
                // Determine if this def toggles recipes
                return (
                    ( !recipeDefs.NullOrEmpty() )&&
                    ( sowTags.NullOrEmpty() )&&
                    ( !thingDefs.NullOrEmpty() )
                );
            }
        }

        public bool                         IsPlantToggle
        {
            get
            {
                // Determine if this def toggles plant sow tags
                return (
                    ( recipeDefs.NullOrEmpty() )&&
                    ( !sowTags.NullOrEmpty() )&&
                    ( !thingDefs.NullOrEmpty() )
                );
            }
        }

        public bool                         IsBuildingToggle
        {
            get
            {
                // Determine if this def toggles buildings
                return (
                    ( recipeDefs.NullOrEmpty() )&&
                    ( sowTags.NullOrEmpty() )&&
                    ( !thingDefs.NullOrEmpty() )
                );
            }
        }

        public bool                         IsResearchToggle
        {
            get
            {
                // Determine if this def toggles research
                return (
                    ( !effectedResearchDefs.NullOrEmpty() )
                );
            }
        }

        public bool                         HasCallbacks
        {
            get
            {
                // Determine if this def has callbacks
                return (
                    ( !researchMods.NullOrEmpty() )
                );
            }
        }

        public bool                         HasHelp
        {
            get
            {
                return
                    (
                        ( ConsolidateHelp )&&
                        ( Priority == HelpConsolidatePriority )
                    )||
                    ( HelpConsolidator != null );
            }
        }

        bool                                HasMatchingResearch( AdvancedResearchDef other )
        {
            if( researchDefs.Count != other.researchDefs.Count )
            {
                return false;
            }

            SortResearch();
            other.SortResearch();

            for( int i = 0; i < researchDefs.Count; ++i )
            {
                if( researchDefs[ i ] != other.researchDefs[ i ] )
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Total research cost of all required researches
        /// </summary>
        public float                        TotalCost
        {
            get
            {
                if( totalCost < 0 )
                {
                    totalCost = researchDefs.Sum( rd => rd.baseCost );
                }
                return totalCost;
            }
        }

        

        public AdvancedResearchDef          HelpConsolidator
        {
            get
            {
                if(
                    ( ConsolidateHelp )&&
                    ( Priority == HelpConsolidatePriority )
                )
                {
                    return this;
                }
                if( helpConsolidator == null )
                {
                    var matching = MatchingAdvancedResearch;
                    if(
                        ( matching.NullOrEmpty() )||
                        (
                            ( matching[ 0 ].Priority != HelpConsolidatePriority )||
                            ( !matching[ 0 ].ConsolidateHelp )
                        )
                    )
                    {
                        return null;
                    }
                    helpConsolidator = matching[ 0 ];
                }
                return helpConsolidator;
            }
        }

        #endregion

        #region Process State

        public void                         ResetOnGameLoad()
        {
            //CCL_Log.Message( string.Format( "Resetting '{0}', HideDefs = '{1}'", this.defName, this.HideDefs ), "AdvancedResearchDef" );
            researchState = ResearchEnableMode.Incomplete;
            if( !HideDefs )
            {
                this.Disable( true );
            }
        }

        public void                         Disable( bool firstTimeRun = false )
        {
            // Don't disable if it's not the first run and not yet enabled
            if(
                ( researchState == ResearchEnableMode.Incomplete )&&
                ( firstTimeRun == false )
            )
            {
                return;
            }
#if DEVELOPER
            CCL_Log.TraceMod(
                modHelperDef,
                Verbosity.StateChanges,
                "Disabling '" + defName + "'",
                "AdvancedResearchDef"
            );
#endif
            if( IsRecipeToggle )
            {
                // Recipe toggle
                ToggleRecipes( true );
            }
            if( IsPlantToggle )
            {
                // Plant toggle
                TogglePlants( true );
            }
            if( IsBuildingToggle )
            {
                // Building toggle
                ToggleBuildings( true );
            }
            if(
                ( IsResearchToggle )&&
                ( researchState != ResearchEnableMode.GodMode )
            )
            {
                // Research toggle
                ToggleResearch( true );
            }
            if(
                ( HasCallbacks )&&
                ( !firstTimeRun )
            )
            {
                // Cache callbacks
                ToggleCallbacks( true );
            }
            if( HasHelp )
            {
                // Build & toggle help
                ToggleHelp( true );
            }
            // Flag it as disabled
            researchState = ResearchEnableMode.Incomplete;
        }

        public void                         Enable( ResearchEnableMode mode )
        {
            // Don't enable if enabled
            if( researchState != ResearchEnableMode.Incomplete )
            {
                if(
                    ( researchState == ResearchEnableMode.GodMode )&&
                    ( mode != ResearchEnableMode.GodMode )&&
                    ( IsResearchToggle )
                )
                {
                    // Player completed research with god-mode on
                    // Research toggle
                    ToggleResearch();
                    // Flag it as enabled by mode
                    researchState = mode;
                }
                return;
            }
#if DEVELOPER
            CCL_Log.TraceMod(
                modHelperDef,
                Verbosity.StateChanges,
                "Enabling '" + defName + "'",
                "AdvancedResearchDef"
            );
#endif
            if( IsRecipeToggle )
            {
                // Recipe toggle
                ToggleRecipes();
            }
            if( IsPlantToggle )
            {
                // Plant toggle
                TogglePlants();
            }
            if( IsBuildingToggle )
            {
                // Building toggle
                ToggleBuildings();
            }
            if(
                ( IsResearchToggle )&&
                ( mode != ResearchEnableMode.GodMode )
            )
            {
                // Research toggle, if it's not god mode
                ToggleResearch();
            }
            if( HasCallbacks )
            {
                // Cache callbacks
                ToggleCallbacks();
            }
            if( HasHelp )
            {
                // Build & toggle help
                ToggleHelp();
            }
            // Flag it as enabled by mode
            researchState = mode;
        }

        void                                SortResearch()
        {
            if( researchSorted )
            {
                return;
            }
            researchDefs.Sort( delegate ( ResearchProjectDef x, ResearchProjectDef y )
                {
                    return x.defName.CompareTo( y.defName ) * -1;
                }
            );
            researchSorted = true;
        }

        #endregion

        #region Toggle States

        void                                ToggleRecipes( bool setInitialState = false )
        {
            bool Hide = !setInitialState ? HideDefs : !HideDefs;

            // Go through each building
            foreach( var buildingDef in thingDefs )
            {

                // Make sure the thing has a recipe list
                if( buildingDef.recipes == null )
                {
                    buildingDef.recipes = new List<RecipeDef>();
                }

                // Go through each recipe
                foreach( var recipeDef in recipeDefs )
                {

                    if( Hide )
                    {
                        // Hide recipe

                        // Remove building from recipe
                        if( recipeDef.recipeUsers != null )
                        {
                            recipeDef.recipeUsers.Remove( buildingDef );
                        }

                        // Remove recipe from building
                        buildingDef.recipes.Remove( recipeDef );

                    }
                    else
                    {
                        // Add recipe to the building
                        buildingDef.recipes.AddUnique( recipeDef );
                    }
                }

                // Add this building to the list to recache
                ResearchSubController.RecacheBuildingRecipes( buildingDef );
            }
        }

        void                                TogglePlants( bool setInitialState = false )
        {
            bool Hide = !setInitialState ? HideDefs : !HideDefs;

            // Go through each plant
            foreach( var plantDef in thingDefs )
            {

                // Make sure it has a list to modify
                if( plantDef.plant.sowTags == null )
                {
                    plantDef.plant.sowTags = new List<string>();
                }

                // Go through each sowTag
                foreach( var sowTag in sowTags )
                {

                    if( Hide )
                    {
                        // Hide plant
                        plantDef.plant.sowTags.Remove( sowTag );
                    }
                    else
                    {
                        // Show the plant
                        plantDef.plant.sowTags.AddUnique( sowTag );
                    }
                }
            }
        }

        void                                ToggleBuildings( bool setInitialState = false )
        {
            bool Hide = !setInitialState ? HideDefs : !HideDefs;

            // Go through each building
            foreach( var buildingDef in thingDefs )
            {
                buildingDef.researchPrerequisites = Hide ? Research.Lockers : Research.Unlockers;
            }
        }

        void                                ToggleResearch( bool setInitialState = false )
        {
            bool Hide = !setInitialState ? HideDefs : !HideDefs;

            // Go through each research project to be effected
            foreach( var researchProject in effectedResearchDefs )
            {
                // Assign a blank list
                researchProject.prerequisites = new List<ResearchProjectDef>();

                if( Hide )
                {
                    var unlockARDs = Controller.Data.AdvancedResearchDefs.Where( def => (
                        ( def.IsResearchToggle )&&
                        ( !def.HideDefs )&&
                        ( def.effectedResearchDefs.Contains( researchProject ) )
                    ) ).ToList();

                    if( unlockARDs.NullOrEmpty() )
                    {
                        // No unlockers, use locker
                        researchProject.prerequisites.AddUnique( Research.Locker );
                    }
                    else
                    {
                        foreach( var unlockARD in unlockARDs )
                        {
                            researchProject.prerequisites.AddRange( unlockARD.researchDefs );
                        }
                    }
                }
                else if( !HideDefs )
                {
                    // Unlocked, use this ARDs prerequisites
                    researchProject.prerequisites.AddRange( researchDefs );
                }

#if DEVELOPER
                var str = string.Format( "Prerequisites for {0}:", researchProject.defName );
                if( !researchProject.prerequisites.NullOrEmpty() )
                {
                    foreach( var project in researchProject.prerequisites )
                    {
                        str += string.Format( "\n\t{0}", project.defName );
                    }
                }
                Log.Message( str );
#endif
            }

            // Invalidate dictionary
            ResearchProjectDef_Extensions.ClearIsLockedOut();
        }

        void                                ToggleCallbacks( bool setInitialState = false )
        {
            bool Hide = !setInitialState ? HideDefs : !HideDefs;

            if( Hide )
            {
                // Cache the callbacks in reverse order when hiding
                for( int i = researchMods.Count - 1; i >= 0; i-- )
                {
                    var researchMod = researchMods[ i ];
                    // Add the advanced research mod to the cache
                    ResearchSubController.ProcessResearchMod( researchMod );
                }
            }
            else
            {
                // Cache the callbacks in order
                foreach( var researchMod in researchMods )
                {

                    // Add the advanced research mod to the cache
                    ResearchSubController.ProcessResearchMod( researchMod );
                }
            }
        }

        public void                         ToggleHelp( bool setInitialState = false )
        {
            if( ( !ConsolidateHelp )||
                ( HelpDef == null ) )
            {
                return;
            }
            bool Hide = HideUntilResearched && setInitialState;

            if( Hide )
            {
                // Hide it from the help system
                HelpDef.category = (HelpCategoryDef)null;
            }
            else
            {
                // Show it to the help system
                HelpDef.category = helpCategoryDef;
            }

            // Queue for recache
            ResearchSubController.RecacheHelpCategory( helpCategoryDef );
        }

        #endregion

        #region Aggregate Data

        List<AdvancedResearchDef>           MatchingAdvancedResearch
        {
            get
            {
                if( matchingAdvancedResearch == null )
                {
                    // Matching advanced research (by requirements)
                    matchingAdvancedResearch = Controller.Data.AdvancedResearchDefs.FindAll( a => (
                        ( HasMatchingResearch( a ) )
                    ) );
                }
                // return the matching research or null
                return matchingAdvancedResearch;
            }
        }

        public List<Def>                    GetResearchRequirements()
        {
            if( HelpConsolidator != this )
            {
                return HelpConsolidator.GetResearchRequirements();
            }

            // Return the list of research required
            return researchDefs.ConvertAll<Def>( def => (Def)def );
        }

        public List<ThingDef>               GetThingsUnlocked()
        {
            if( HelpConsolidator != this )
            {
                return HelpConsolidator.GetThingsUnlocked();
            }

            // Buildings it unlocks
            var thingdefs = new List<ThingDef>();

            // Look at this def
            if(
                ( !HideDefs )&&
                ( IsBuildingToggle )
            )
            {
                thingdefs.AddRangeUnique( thingDefs );
            }

            // Look in matching research
            if( !MatchingAdvancedResearch.NullOrEmpty() )
            {
                foreach( var a in MatchingAdvancedResearch )
                {
                    if(
                        ( !a.HideDefs )&&
                        ( a.IsBuildingToggle )
                    )
                    {
                        thingdefs.AddRangeUnique( a.thingDefs );
                    }
                }
            }

            return thingdefs;
        }

        public List<ThingDef>               GetThingsLocked()
        {
            if( HelpConsolidator != this )
            {
                return HelpConsolidator.GetThingsLocked();
            }

            // Buildings it locks
            var thingdefs = new List<ThingDef>();

            // Look at this def
            if(
                ( HideDefs )&&
                ( IsBuildingToggle )
            )
            {
                thingdefs.AddRangeUnique( thingDefs );
            }

            // Look in matching research
            if( !MatchingAdvancedResearch.NullOrEmpty() )
            {
                foreach( var a in MatchingAdvancedResearch )
                {
                    if(
                        ( a.HideDefs )&&
                        ( a.IsBuildingToggle )
                    )
                    {
                        thingdefs.AddRangeUnique( a.thingDefs );
                    }
                }
            }

            return thingdefs;
        }

        public List<RecipeDef>              GetRecipesUnlocked( ref List<ThingDef> thingdefs )
        {
            if( HelpConsolidator != this )
            {
                return HelpConsolidator.GetRecipesUnlocked( ref thingdefs );
            }

            // Recipes on buildings it unlocks
            var recipedefs = new List<RecipeDef>();
            if( thingdefs != null )
            {
                thingdefs.Clear();
            }

            // Look at this def
            if(
                ( !HideDefs )&&
                ( IsRecipeToggle )
            )
            {
                recipedefs.AddRangeUnique( recipeDefs );
                if( thingdefs != null )
                {
                    thingdefs.AddRangeUnique( thingDefs );
                }
            }

            // Look in matching research
            if( !MatchingAdvancedResearch.NullOrEmpty() )
            {
                foreach( var a in MatchingAdvancedResearch )
                {
                    if(
                        ( !a.HideDefs )&&
                        ( a.IsRecipeToggle )
                    )
                    {
                        recipedefs.AddRangeUnique( a.recipeDefs );
                        if( thingdefs != null )
                        {
                            thingdefs.AddRangeUnique( a.thingDefs );
                        }
                    }
                }
            }

            return recipedefs;
        }

        public List<RecipeDef>              GetRecipesLocked( ref List<ThingDef> thingdefs )
        {
            if( HelpConsolidator != this )
            {
                return HelpConsolidator.GetRecipesLocked( ref thingdefs );
            }

            // Recipes on buildings it locks
            var recipedefs = new List<RecipeDef>();
            if( thingdefs != null )
            {
                thingdefs.Clear();
            }

            // Look at this def
            if(
                ( HideDefs )&&
                ( IsRecipeToggle )
            )
            {
                recipedefs.AddRangeUnique( recipeDefs );
                if( thingdefs != null )
                {
                    thingdefs.AddRangeUnique( thingDefs );
                }
            }

            // Look in matching research
            foreach( var a in MatchingAdvancedResearch )
            {
                if(
                    ( a.HideDefs )&&
                    ( a.IsRecipeToggle )
                )
                {
                    recipedefs.AddRangeUnique( a.recipeDefs );
                    if( thingdefs != null )
                    {
                        thingdefs.AddRangeUnique( a.thingDefs );
                    }
                }
            }

            return recipedefs;
        }

        public List<string>                 GetSowTagsUnlocked( ref List<ThingDef> thingdefs )
        {
            if( HelpConsolidator != this )
            {
                return HelpConsolidator.GetSowTagsUnlocked( ref thingdefs );
            }

            // Sow tags on plants it unlocks
            var sowtags = new List<string>();
            if( thingdefs != null )
            {
                thingdefs.Clear();
            }

            // Look at this def
            if(
                ( !HideDefs )&&
                ( IsPlantToggle )
            )
            {
                sowtags.AddRangeUnique( sowTags );
                if( thingdefs != null )
                {
                    thingdefs.AddRangeUnique( thingDefs );
                }
            }

            // Look in matching research
            if( !MatchingAdvancedResearch.NullOrEmpty() )
            {
                foreach( var a in MatchingAdvancedResearch )
                {
                    if(
                        ( !a.HideDefs )&&
                        ( a.IsPlantToggle )
                    )
                    {
                        sowtags.AddRangeUnique( a.sowTags );
                        if( thingdefs != null )
                        {
                            thingdefs.AddRangeUnique( a.thingDefs );
                        }
                    }
                }
            }

            return sowtags;
        }

        public List<string>                 GetSowTagsLocked( ref List<ThingDef> thingdefs )
        {
            if( HelpConsolidator != this )
            {
                return HelpConsolidator.GetSowTagsLocked( ref thingdefs );
            }

            // Sow tags on plants it unlocks
            var sowtags = new List<string>();
            if( thingdefs != null )
            {
                thingdefs.Clear();
            }

            // Look at this def
            if(
                ( HideDefs )&&
                ( IsPlantToggle )
            )
            {
                sowtags.AddRangeUnique( sowTags );
                if( thingdefs != null )
                {
                    thingdefs.AddRangeUnique( thingDefs );
                }
            }

            // Look in matching research
            if( !MatchingAdvancedResearch.NullOrEmpty() )
            {
                foreach( var a in MatchingAdvancedResearch )
                {
                    if(
                        ( a.HideDefs )&&
                        ( a.IsPlantToggle )
                    )
                    {
                        sowtags.AddRangeUnique( a.sowTags );
                        if( thingdefs != null )
                        {
                            thingdefs.AddRangeUnique( a.thingDefs );
                        }
                    }
                }
            }

            return sowtags;
        }

        public List<Def>                    GetResearchUnlocked()
        {
            if( HelpConsolidator != this )
            {
                return HelpConsolidator.GetResearchUnlocked();
            }

            // Research it unlocks
            var researchdefs = new List<Def>();

            // Look at this def
            if(
                ( !HideDefs )&&
                ( IsResearchToggle )
            )
            {
                researchdefs.AddRangeUnique( effectedResearchDefs.ConvertAll<Def>( def => (Def)def ) );
            }

            // Look in matching research
            if( !MatchingAdvancedResearch.NullOrEmpty() )
            {
                foreach( var a in MatchingAdvancedResearch )
                {
                    if(
                        ( !a.HideDefs )&&
                        ( a.IsResearchToggle )
                    )
                    {
                        researchdefs.AddRangeUnique( a.effectedResearchDefs.ConvertAll<Def>( def => (Def)def ) );
                    }
                }
            }

            return researchdefs;
        }

        public List<Def>                    GetResearchLocked()
        {
            if( HelpConsolidator != this )
            {
                return HelpConsolidator.GetResearchLocked();
            }

            // Research it locks
            var researchdefs = new List<Def>();

            // Look at this def
            if(
                ( HideDefs )&&
                ( IsResearchToggle )
            )
            {
                researchdefs.AddRangeUnique( effectedResearchDefs.ConvertAll<Def>( def => (Def)def ) );
            }

            // Look in matching research
            if( !MatchingAdvancedResearch.NullOrEmpty() )
            {
                foreach( var a in MatchingAdvancedResearch )
                {
                    if(
                        ( a.HideDefs )&&
                        ( a.IsResearchToggle )
                    )
                    {
                        researchdefs.AddRangeUnique( a.effectedResearchDefs.ConvertAll<Def>( def => (Def)def ) );
                    }
                }
            }

            return researchdefs;
        }

        #endregion

        #region Help Def

        public HelpDef                      HelpDef
        {
            get
            {
                if( helpDef != null )
                {
                    return helpDef;
                }

                if( HelpConsolidator != null )
                {
                    return HelpConsolidator.helpDef;
                }
                return null;
            }
            set
            {
                if( ConsolidateHelp )
                {
                    helpDef = value;
                }
            }
        }

        #endregion

        #region ResearchProjectDef 'interface'

        public bool                         IsFinished
        {
            get
            {
                return ( ResearchState == ResearchEnableMode.Complete );
            }
        }
        
        #endregion

    }

}
