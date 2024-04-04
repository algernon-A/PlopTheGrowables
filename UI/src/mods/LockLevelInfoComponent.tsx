import { useLocalization } from "cs2/l10n";
import { bindValue, trigger, useValue } from "cs2/api";
import { selectedInfo, Entity } from "cs2/bindings";
import { VanillaComponentResolver } from "./VanillaComponentResolver";
import {compile} from "sass";

// Currently selected entity.
export var selectedEntity: Entity;

export const selectedEntity$ = selectedInfo.selectedEntity$;
export const isBuildingLocked$ = bindValue<boolean>('PlopTheGrowables', 'IsBuildingLocked');

// Translation.
export function translate(key: string) {
    const { translate } = useLocalization();
    return translate (key);
}

// Selected entity change handler.
export function SelectedEntityChanged(newEntity: Entity) {
    selectedEntity = newEntity;
    trigger("PlopTheGrowables", "SelectedEntity", newEntity);
}

// Current level locked status.
export function IsLevelLocked() : boolean {
    return useValue(isBuildingLocked$);
}

// Event handler.
export function lockLevelClick() {
    trigger("PlopTheGrowables", "ToggleLockLevel", selectedEntity);
}

export const LockLevelInfoComponent = (componentList: any): any => {
    selectedInfo.selectedEntity$.subscribe(SelectedEntityChanged);

    //const LevelSectionModule = registry.get("game-ui/game/components/selected-info-panel/selected-info-sections/building-sections/level-section/level-section.tsx");
    
    // Titled tooltip generator.
    function  TitledTooltip (titleKey: string, contentKey: string): JSX.Element {
        return (
            <>
                <div className={VanillaComponentResolver.instance.descriptionTooltipTheme.title}>{translate(titleKey)}</div>
                <div className={VanillaComponentResolver.instance.descriptionTooltipTheme.content}>{translate(contentKey)}</div>
            </>
        )
    }
    
    // Add lock level info panel row.
    componentList["PlopTheGrowables.LockLevel"] = () =>
        <VanillaComponentResolver.instance.InfoSection>
            <VanillaComponentResolver.instance.InfoRow
                left={translate("PLOPTHEGROWABLES.LockLevel")}
                uppercase = {true}
                right={
                    <VanillaComponentResolver.instance.ToolButton
                        className={VanillaComponentResolver.instance.toolButtonTheme.button}
                        src={IsLevelLocked() ? "Media/Glyphs/Lock.svg" : "Media/Tools/Net Tool/Point.svg"}
                        tooltip={TitledTooltip("PLOPTHEGROWABLES.LockLevel", "PLOPTHEGROWABLES_DESCRIPTION.LockLevel")}
                        onSelect={lockLevelClick}
                        selected={IsLevelLocked()}
                        multiSelect={false}
                        disabled={false}
                        focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED}
                    />
                }
            />
        </VanillaComponentResolver.instance.InfoSection>
    
    return componentList as any;
}