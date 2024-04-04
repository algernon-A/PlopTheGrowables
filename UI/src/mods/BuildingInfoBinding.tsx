import { selectedInfo } from "cs2/bindings";

let currentEntity: any = null;
const selectedEntity$ = selectedInfo.selectedEntity$;
const middleSections$ = selectedInfo.middleSections$;
let lastMiddleSection: any = null;

export const BuildingInfoBinding = () => {
    selectedEntity$.subscribe((entity) => {
        if (!entity.index) {
            currentEntity = null;
            return entity
        }
        if (currentEntity != entity.index) {
            currentEntity = entity.index
        }
        return entity;
    })

    // Add lock level section to end of info panel middle section.
    middleSections$.subscribe((val) => {
        if (currentEntity && val.every(x => x?.__Type != "PlopTheGrowables.LockLevel" as any)) {
            
            // Only add if there's a building level section.
            if (val.find(x => x?.__Type == "Game.UI.InGame.LevelSection" as any)) {
                val.unshift({
                    __Type: "PlopTheGrowables.LockLevel",
                    group: 'LevelSection'
                } as any);
            }
        }

        return lastMiddleSection = val;
    })
    return <></>;
}