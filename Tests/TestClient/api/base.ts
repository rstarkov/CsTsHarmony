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

    protected static async parse(resp: APIResponse): Promise<any> {
        if (resp.status() < 200 || resp.status() >= 300)
            throw new Error();
        if (resp.headers()['content-length'] == '0')
            return undefined;
        if (resp.headers()['content-type']?.startsWith('application/json'))
            return await resp.json();
        return await resp.text();
    }

    protected async GET<T>(url: string, init?: RequestArgs): Promise<T> {
        let resp = await this.request.get(url, { data: init?.body, headers: init?.headers });
        return ApiServiceBase.parse(resp) as any;
    }

    protected async POST<T>(url: string, init?: RequestArgs): Promise<T> {
        let resp = await this.request.post(url, { data: init?.body, headers: init?.headers });
        return ApiServiceBase.parse(resp) as any;
    }
}
