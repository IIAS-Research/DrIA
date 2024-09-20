import { MigrationInterface, QueryRunner } from 'typeorm';

export class AddFieldIsadminToUsers1726753001170 implements MigrationInterface {
  name = 'AddFieldIsadminToUsers1726753001170';

  public async up(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(
      `ALTER TABLE "users" ADD "isAdmin" boolean NOT NULL DEFAULT false`,
    );
  }

  public async down(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(`ALTER TABLE "users" DROP COLUMN "isAdmin"`);
  }
}
