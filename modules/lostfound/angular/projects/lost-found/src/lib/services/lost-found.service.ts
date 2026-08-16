import { inject, Injectable } from '@angular/core';
import { RestService } from '@abp/ng.core';

@Injectable({
  providedIn: 'root',
})
export class LostFoundService {
  apiName = 'LostFound';

  private restService = inject(RestService);

  sample() {
    return this.restService.request<void, any>(
      { method: 'GET', url: '/api/lost-found/example' },
      { apiName: this.apiName }
    );
  }
}
