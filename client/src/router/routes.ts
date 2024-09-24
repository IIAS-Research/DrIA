import { RouteRecordRaw } from 'vue-router';
import { mustBeAdmin, mustBeAuth, mustNotBeAuth } from './routesGuards';

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('pages/LoginPage.vue'),
    beforeEnter: mustNotBeAuth,
  },
  {
    path: '/',
    beforeEnter: mustBeAuth,
    children: [
      {
        path: '',
        name: 'home',
        component: () => import('pages/ProjectbuildPage.vue'),
      },
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
