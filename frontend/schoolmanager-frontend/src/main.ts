import { enableProdMode } from '@angular/core';

import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';
import { AppModule } from './app/app.module';

import { bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';
import { appConfig } from './app/app.config';

import { environment } from './app/environments/environment';

if (environment.production) {
  enableProdMode();
}


platformBrowserDynamic()
  .bootstrapModule(AppModule)

bootstrapApplication(App, appConfig)

  .catch(err => console.error(err));
