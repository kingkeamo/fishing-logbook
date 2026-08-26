export class TimeoutError extends Error {
    constructor(message) {
        super(message);
        this.name = 'TimeoutError';
    }
}

export function withTimeout(promise, milliseconds, operationName) {
    return new Promise((resolve, reject) => {
        const timer = setTimeout(
            () => reject(new TimeoutError(`${operationName} timed out`)),
            milliseconds);
        promise.then(
            (value) => {
                clearTimeout(timer);
                resolve(value);
            },
            (error) => {
                clearTimeout(timer);
                reject(error);
            });
    });
}
