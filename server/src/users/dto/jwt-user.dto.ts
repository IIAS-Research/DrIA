import { User } from '../entities/user.entity';

export class JwtUserDto {
  email: User['email'];
  sub: User['id'];
}
