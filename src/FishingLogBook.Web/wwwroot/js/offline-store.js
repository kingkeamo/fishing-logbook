export {
    putTestCatch,
    getAllTestCatches,
    putCatchWithPhotographs,
    getAllCatchesWithPhotographs,
    updateCatchMetadata,
    putFishingPreferences,
    getFishingPreferences,
    clearFishingPreferences
} from './storage/catch-store.js';
export { putTestCatchPhotograph, getTestCatchPhotograph } from './storage/photo-store.js';
export { getStorageEstimate } from './storage/indexed-db.js';
