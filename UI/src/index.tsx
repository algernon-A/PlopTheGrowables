import { ModRegistrar } from "cs2/modding";
import {LockLevelInfoComponent} from "./mods/LockLevelInfoComponent";
import {BuildingInfoBinding} from "./mods/BuildingInfoBinding";
import {VanillaComponentResolver} from "./mods/VanillaComponentResolver";


const register: ModRegistrar = (moduleRegistry) => {
    
    // Set registry singleton reference.
    VanillaComponentResolver.setRegistry(moduleRegistry);

    // Insert lock level button inI'd I's to building info panel.
    moduleRegistry.append('Game', BuildingInfoBinding);
    moduleRegistry.extend("game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", 'selectedInfoSectionComponents', LockLevelInfoComponent)
}

export default register;