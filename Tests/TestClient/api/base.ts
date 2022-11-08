import { APIRequestContext, APIResponse } from '@playwright/test';

export interface ApiServiceOptions {
    request: APIRequestContext;
}

interface RequestArgs {
    body: string;
    headers: { [key: string]: string };
}

export class ApiServiceBase {

    protected request: APIRequestContext;

    public constructor(options?: ApiServiceOptions) {
        if (!options?.request)
            throw new Error();
        this.request = options.request;
    }

    protected static async parse<T>(resp: APIResponse): Promise<T> {
        if (resp.status() == 204) // WebAPI returns 204 for void-returning methods, and such a response can't be parsed by resp.json()
            return undefined as any;
        let json: any = await resp.json();
        if (resp.status() == 200)
            return json;
        throw new Error();
    }

    protected async GET<T>(url: string, init?: RequestArgs): Promise<T> {
        let resp = await this.request.get(url, { data: init?.body, headers: init?.headers });
        return ApiServiceBase.parse<T>(resp);
    }

    protected async POST<T>(url: string, init?: RequestArgs): Promise<T> {
        let resp = await this.request.post(url, { data: init?.body, headers: init?.headers });
        return ApiServiceBase.parse<T>(resp);
    }
}
