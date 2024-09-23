import { RouteRecordRaw } from 'vue-router';
import { mustBeAdmin } from './routesGuards';

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    component: () => import('layouts/MainLayout.vue'),
    children: [
      { path: '', name: 'home', component: () => import('pages/IndexPage.vue') },
      {
        path: 'manage-users',
        name: 'admin-users',
        component: () => import('pages/administration/ManageUsersPage.vue'),
        beforeEnter: mustBeAdmin,
      },
    ],
  },

  // Always leave this as last one,
  // but you can also remove it
  {
    path: '/:catchAll(.*)*',
    name: 'error',
    component: () => import('pages/ErrorNotFound.vue'),
  },
];

export default routes;
